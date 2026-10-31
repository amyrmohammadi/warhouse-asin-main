using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.Invoices.Commands.UpdateInvoiceItem;
using Warehouse.Application.Features.Inventory.Invoices.Commands.UpdateInvoice;
using Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoiceById;
using Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoices;
using Warehouse.Server.Controllers;
using Warehouse.Server.Contracts.Inventory;
using Warehouse.Domain.Enums;
using Warehouse.Application.Features.Inventory.Invoices.Commands.PrintInvoices;
using Warehouse.Application.Features.Inventory.Invoices.Commands.ChangeInvoiceStatus;

namespace Warehouse.Server.Tests.Controllers;

public sealed class InvoicesControllerTests
{
    [Theory]
    [InlineData(InvoiceProcessingStatus.UnderReview, PermissionPolicyNames.Inventory.GoodsReceiptsReceiptInvoiceOperations, false)]
    [InlineData(InvoiceProcessingStatus.UnderReview, PermissionPolicyNames.Inventory.InvoicesReopen, true)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved, PermissionPolicyNames.Inventory.InvoicesReopen, false)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved, PermissionPolicyNames.Inventory.GoodsReceiptsReceiptInvoiceOperations, true)]
    public async Task ChangeStatus_EnforcesDedicatedReopenPermission(InvoiceProcessingStatus target, string granted, bool allowed)
    {
        var sender = Substitute.For<ISender>();
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<string>(2) == granted ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        sender.Send(Arg.Any<ChangeInvoiceStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<InvoiceDetails>.Success(new() { ProcessingStatus = target }));
        var controller = new InvoicesController(sender, auth)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var id = Guid.NewGuid();
        var response = await controller.ChangeStatus(id, new(target, InvoiceProcessingStatus.SentToFinance), default);
        if (allowed)
        {
            Assert.IsType<OkObjectResult>(response.Result);
            await sender.Received(1).Send(Arg.Is<ChangeInvoiceStatusCommand>(command =>
                command.InvoiceId == id && command.Status == target && command.ExpectedStatus == InvoiceProcessingStatus.SentToFinance &&
                command.CanReopen == (target == InvoiceProcessingStatus.UnderReview) && command.CanApprove == (target == InvoiceProcessingStatus.FinanceApproved)),
                Arg.Any<CancellationToken>());
        }
        else
        {
            Assert.IsType<ForbidResult>(response.Result);
            await sender.DidNotReceive().Send(Arg.Any<ChangeInvoiceStatusCommand>(), Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Print_RequiresEditPermissionOnlyWhenSendingToFinance(bool sendToFinance, bool canEdit)
    {
        Assert.Equal(PermissionPolicyNames.Inventory.InvoicesPrint,
            typeof(InvoicesController).GetMethod(nameof(InvoicesController.Print))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy);
        var sender = Substitute.For<ISender>();
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(canEdit ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        sender.Send(Arg.Any<PrintInvoicesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Success([1, 2, 3]));
        var controller = new InvoicesController(sender, auth)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var response = await controller.Print(new(ids, sendToFinance), default);
        if (sendToFinance && !canEdit)
        {
            Assert.IsType<ForbidResult>(response);
            await sender.DidNotReceive().Send(Arg.Any<PrintInvoicesCommand>(), Arg.Any<CancellationToken>());
        }
        else
        {
            Assert.Equal("application/pdf", Assert.IsType<FileContentResult>(response).ContentType);
            Assert.Equal("no-store", controller.Response.Headers.CacheControl);
            await sender.Received(1).Send(Arg.Is<PrintInvoicesCommand>(command => command.InvoiceIds.SequenceEqual(ids) &&
                command.SendToFinance == sendToFinance), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task Print_MissingFieldsReturnsAllValidationMessagesInsteadOfPdf()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<PrintInvoicesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Error("مبلغ فاکتور ۱ وارد نشده", "طرف حساب فاکتور ۲ مشخص نیست"));
        var controller = new InvoicesController(sender, Substitute.For<IAuthorizationService>())
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var response = await controller.Print(new([Guid.NewGuid()], false), default);
        var error = Assert.IsType<Result<byte[]>>(Assert.IsType<BadRequestObjectResult>(response).Value);
        Assert.Equal(2, error.Messages.Count);
    }

    [Fact]
    public void Controller_UsesDedicatedInvoiceReadPolicy()
    {
        var authorize = typeof(InvoicesController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(PermissionPolicyNames.Inventory.InvoicesRead, authorize.Policy);
    }

    [Fact]
    public void UpdateItem_UsesReceiptRegistrationPolicy()
    {
        var action = typeof(InvoicesController).GetMethod(nameof(InvoicesController.UpdateItem))!;
        var authorize = action.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(
            PermissionPolicyNames.Inventory.GoodsReceiptsReceiptRegistration,
            authorize.Policy);
    }

    [Fact]
    public async Task Update_ForwardsAllInvoiceFieldsAndUsesEditPolicy()
    {
        var action = typeof(InvoicesController).GetMethod(nameof(InvoicesController.Update))!;
        Assert.Null(action.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        var sender = Substitute.For<ISender>();
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(AuthorizationResult.Success());
        var invoiceId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var stamp = Guid.NewGuid();
        sender.Send(Arg.Any<UpdateInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<InvoiceDetails>.Success(new InvoiceDetails { Id = invoiceId }));
        var controller = new InvoicesController(sender, auth)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var supplierId = Guid.NewGuid();
        var request = new UpdateInvoiceRequest("INV-2", supplierId, "شرح", new DateOnly(2026, 10, 27),
            InvoiceProcessingStatus.FinanceApproved,
            [new UpdateInvoiceReceiptItemRequest(itemId, "REC-1", stamp, 125)], Price: 500);

        var response = await controller.Update(invoiceId, request, default);

        Assert.IsType<OkObjectResult>(response.Result);
        await sender.Received(1).Send(Arg.Is<UpdateInvoiceCommand>(command =>
            command.InvoiceId == invoiceId && command.InvoiceNumber == "INV-2" &&
            command.SupplierId == supplierId && command.Description == "شرح" &&
            command.InvoiceDate == request.InvoiceDate &&
            command.ProcessingStatus == InvoiceProcessingStatus.FinanceApproved &&
            command.Items.Count == 1 && command.Items.Single().Id == itemId &&
            command.Items.Single().ReceiptNumber == "REC-1" &&
            command.Items.Single().GoodsReceiptConcurrencyStamp == stamp &&
            command.Items.Single().Price == 125 && command.Price == 500 &&
            command.CanEditInvoice && command.CanEditReceipt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithoutInvoiceOrReceiptPermission_IsForbidden()
    {
        var sender = Substitute.For<ISender>();
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(AuthorizationResult.Failed());
        var controller = new InvoicesController(sender, auth)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var request = new UpdateInvoiceRequest("INV-1", null, null, null,
            InvoiceProcessingStatus.UnderReview, []);

        var response = await controller.Update(Guid.NewGuid(), request, default);

        Assert.IsType<ForbidResult>(response.Result);
        await sender.DidNotReceive().Send(Arg.Any<UpdateInvoiceCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithReceiptPermission_OnlyForwardsReceiptEditPermission()
    {
        var sender = Substitute.For<ISender>();
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(call => call.Arg<string>() == PermissionPolicyNames.Inventory.GoodsReceiptsReceiptRegistration
                ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        sender.Send(Arg.Any<UpdateInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<InvoiceDetails>.Success(new InvoiceDetails()));
        var controller = new InvoicesController(sender, auth)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var request = new UpdateInvoiceRequest("IGNORED", null, null, null,
            InvoiceProcessingStatus.UnderReview, []);

        var response = await controller.Update(Guid.NewGuid(), request, default);

        Assert.IsType<OkObjectResult>(response.Result);
        await sender.Received(1).Send(Arg.Is<UpdateInvoiceCommand>(command =>
            !command.CanEditInvoice && command.CanEditReceipt), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetById_ForwardsInvoiceIdAndReturnsResult()
    {
        var sender = Substitute.For<ISender>();
        var id = Guid.NewGuid();
        var expected = Result<InvoiceDetails>.Success(new InvoiceDetails
        {
            Id = id,
            InvoiceNumber = "INV-1"
        });
        sender.Send(
                Arg.Is<GetInvoiceByIdQuery>(query => query.Id == id),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var controller = new InvoicesController(sender, Substitute.For<IAuthorizationService>());

        var response = await controller.GetById(id, default);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public async Task GetAll_ForwardsPagingAndFilters()
    {
        var sender = Substitute.For<ISender>();
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(),
                PermissionPolicyNames.Inventory.InvoicesMissingReceiptCountRead)
            .Returns(AuthorizationResult.Success());
        var expected = Result<InvoiceListResult>.Success(new InvoiceListResult([], 0, 0, 2, 25));
        sender.Send(Arg.Any<GetInvoicesQuery>(), Arg.Any<CancellationToken>()).Returns(expected);
        var controller = new InvoicesController(sender, auth)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var query = new GetInvoicesQuery { PageNumber = 2, PageSize = 25, Search = "INV" };

        var response = await controller.GetAll(query, default);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, ok.Value);
        await sender.Received(1).Send(query, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAll_OnlyReturnsMissingReceiptCountWithDedicatedPermission(bool canViewCount)
    {
        var sender = Substitute.For<ISender>();
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(),
                PermissionPolicyNames.Inventory.InvoicesMissingReceiptCountRead)
            .Returns(canViewCount ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        var summary = new InvoiceSummary
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-1",
            Description = "شرح",
            InvoiceDate = new DateOnly(2026, 10, 20),
            ProcessingStatus = InvoiceProcessingStatus.SentToFinance,
            Price = 500,
            ReceiptNumbers = ["REC-1"],
            ItemsWithoutReceiptCount = 2
        };
        sender.Send(Arg.Any<GetInvoicesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<InvoiceListResult>.Success(new InvoiceListResult([summary], 1, 1, 1, 10)));
        var controller = new InvoicesController(sender, auth)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var response = await controller.GetAll(new GetInvoicesQuery(), default);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var result = Assert.IsType<Result<InvoiceListResult>>(ok.Value);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(canViewCount ? 2 : null, item.ItemsWithoutReceiptCount);
        Assert.Equal(summary.InvoiceNumber, item.InvoiceNumber);
        Assert.Equal(summary.Description, item.Description);
        Assert.Equal(summary.Price, item.Price);
        Assert.Equal(summary.InvoiceDate, item.InvoiceDate);
        Assert.Equal(summary.ProcessingStatus, item.ProcessingStatus);
        Assert.Equal(summary.ReceiptNumbers, item.ReceiptNumbers);
        var json = JsonSerializer.Serialize(item);
        Assert.Equal(canViewCount, json.Contains("ItemsWithoutReceiptCount", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpdateItem_WithConcurrencyConflict_ReturnsHttp409()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<UpdateInvoiceItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<UpdateInvoiceItemResult>.Error(ConcurrencyMessages.Conflict));
        var controller = new InvoicesController(sender, Substitute.For<IAuthorizationService>());

        var response = await controller.UpdateItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new UpdateInvoiceItemRequest("REC-1", Guid.NewGuid()),
            default);

        Assert.IsType<ConflictObjectResult>(response.Result);
    }
}
