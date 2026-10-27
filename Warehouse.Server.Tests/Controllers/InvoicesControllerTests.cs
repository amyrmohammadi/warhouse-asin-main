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

namespace Warehouse.Server.Tests.Controllers;

public sealed class InvoicesControllerTests
{
    [Fact]
    public void Controller_UsesDedicatedInvoiceReadPolicy()
    {
        var authorize = typeof(InvoicesController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(PermissionPolicyNames.Inventory.InvoicesRead, authorize.Policy);
    }

    [Fact]
    public void UpdateItem_UsesReceiptAndInvoiceOperationsPolicy()
    {
        var action = typeof(InvoicesController).GetMethod(nameof(InvoicesController.UpdateItem))!;
        var authorize = action.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(
            PermissionPolicyNames.Inventory.GoodsReceiptsReceiptInvoiceOperations,
            authorize.Policy);
    }

    [Fact]
    public async Task Update_ForwardsAllInvoiceFieldsAndUsesEditPolicy()
    {
        var action = typeof(InvoicesController).GetMethod(nameof(InvoicesController.Update))!;
        Assert.Equal(PermissionPolicyNames.Inventory.GoodsReceiptsReceiptInvoiceOperations,
            action.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        var sender = Substitute.For<ISender>();
        var invoiceId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var stamp = Guid.NewGuid();
        sender.Send(Arg.Any<UpdateInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<InvoiceDetails>.Success(new InvoiceDetails { Id = invoiceId }));
        var controller = new InvoicesController(sender, Substitute.For<IAuthorizationService>());
        var supplierId = Guid.NewGuid();
        var request = new UpdateInvoiceRequest("INV-2", supplierId, "شرح", new DateOnly(2026, 10, 27),
            InvoiceProcessingStatus.FinanceApproved,
            [new UpdateInvoiceReceiptItemRequest(itemId, "REC-1", stamp)]);

        var response = await controller.Update(invoiceId, request, default);

        Assert.IsType<OkObjectResult>(response.Result);
        await sender.Received(1).Send(Arg.Is<UpdateInvoiceCommand>(command =>
            command.InvoiceId == invoiceId && command.InvoiceNumber == "INV-2" &&
            command.SupplierId == supplierId && command.Description == "شرح" &&
            command.InvoiceDate == request.InvoiceDate &&
            command.ProcessingStatus == InvoiceProcessingStatus.FinanceApproved &&
            command.Items.Count == 1 && command.Items.Single().Id == itemId &&
            command.Items.Single().ReceiptNumber == "REC-1" &&
            command.Items.Single().GoodsReceiptConcurrencyStamp == stamp),
            Arg.Any<CancellationToken>());
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
