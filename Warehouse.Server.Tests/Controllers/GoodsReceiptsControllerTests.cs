using System.Security.Claims;
using System.Reflection;
using ClosedXML.Excel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Auditing;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Auditing.Queries.GetGoodsReceiptItemHistory;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.UpdateGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptItemRows;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptMetrics;
using Warehouse.Domain.Enums;
using Warehouse.Server.Contracts.Inventory;
using Warehouse.Server.Controllers;

namespace Warehouse.Server.Tests.Controllers;

public sealed class GoodsReceiptsControllerTests
{
    [Fact]
    public async Task GetMetrics_OnlyRequestsCountsAllowedByIndividualPolicies()
    {
        var sender = Substitute.For<ISender>();
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<string>(2) switch
            {
                PermissionPolicyNames.Inventory.GoodsReceiptsMissingReceiptCountRead => AuthorizationResult.Success(),
                PermissionPolicyNames.Inventory.GoodsReceiptsTodayDocumentCountRead => AuthorizationResult.Success(),
                _ => AuthorizationResult.Failed()
            });
        sender.Send(Arg.Any<GetGoodsReceiptMetricsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<GoodsReceiptMetrics>.Success(new(4, null, null, 2)));
        var controller = new GoodsReceiptsController(sender, auth)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var response = await controller.GetMetrics(default);

        Assert.IsType<OkObjectResult>(response.Result);
        await sender.Received(1).Send(Arg.Is<GetGoodsReceiptMetricsQuery>(query =>
            query.IncludeMissingReceipt && !query.IncludeMissingInvoice &&
            !query.IncludeTodayItems && query.IncludeTodayDocuments), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(nameof(GoodsReceiptsController.Import), PermissionPolicyNames.Inventory.GoodsReceiptsImport)]
    [InlineData(nameof(GoodsReceiptsController.ExportItems), PermissionPolicyNames.Inventory.GoodsReceiptsExport)]
    [InlineData(nameof(GoodsReceiptsController.GetItemHistory), PermissionPolicyNames.Inventory.GoodsReceiptHistoryRead)]
    [InlineData(nameof(GoodsReceiptsController.GetMetrics), PermissionPolicyNames.Inventory.GoodsReceiptsRead)]
    public void ImportAndExport_UseTheirDedicatedPolicies(string actionName, string expectedPolicy)
    {
        var action = typeof(GoodsReceiptsController).GetMethod(actionName)!;
        var authorize = action.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(expectedPolicy, authorize.Policy);
    }

    [Fact]
    public async Task GetItemHistory_ForwardsItemIdAndReturnsResult()
    {
        var sender = Substitute.For<ISender>();
        var auth = Substitute.For<IAuthorizationService>();
        var itemId = Guid.NewGuid();
        var expected = Result<GoodsReceiptItemHistoryResult>.Success(
            new GoodsReceiptItemHistoryResult(itemId, "کالا", []));
        sender.Send(
                Arg.Is<GetGoodsReceiptItemHistoryQuery>(query => query.ItemId == itemId),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var controller = new GoodsReceiptsController(sender, auth);

        var response = await controller.GetItemHistory(itemId, default);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, ok.Value);
    }

    [Fact]
    public async Task ExportItems_FetchesAllFilteredRowsInFiveHundredRowBatches()
    {
        var sender = Substitute.For<ISender>();
        var auth = Substitute.For<IAuthorizationService>();
        var requests = new List<GetGoodsReceiptItemRowsQuery>();
        var firstBatch = Enumerable.Range(1, 500)
            .Select(index => ExportRow($"کالای {index}"))
            .ToArray();
        var secondBatch = new[] { ExportRow("کالای 501") };

        sender.Send(Arg.Any<GetGoodsReceiptItemRowsQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<GetGoodsReceiptItemRowsQuery>();
                requests.Add(request);
                var items = request.PageNumber == 1 ? firstBatch : secondBatch;
                return Result<GoodsReceiptItemRowListResult>.Success(new GoodsReceiptItemRowListResult(
                    items,
                    501,
                    501,
                    request.PageNumber,
                    request.PageSize));
            });
        var controller = new GoodsReceiptsController(sender, auth)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var response = await controller.ExportItems(
            new GetGoodsReceiptItemRowsQuery { Search = "فیلتر" },
            default);

        var file = Assert.IsType<FileStreamResult>(response);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            file.ContentType);
        Assert.Collection(
            requests,
            request =>
            {
                Assert.Equal(1, request.PageNumber);
                Assert.Equal(500, request.PageSize);
                Assert.Equal("فیلتر", request.Search);
            },
            request =>
            {
                Assert.Equal(2, request.PageNumber);
                Assert.Equal(500, request.PageSize);
                Assert.Equal("فیلتر", request.Search);
            });

        using var workbook = new XLWorkbook(file.FileStream);
        var worksheet = workbook.Worksheet("ورودی کالا");
        Assert.Equal(502, worksheet.LastRowUsed()!.RowNumber());
        Assert.Equal(new[]
        {
            "ردیف", "تاریخ", "عنوان کالا", "کد کالا", "تعداد", "واحد", "توضیحات",
            "تحویل دهنده", "فاکتور", "شماره فاکتور", "طرف حساب", "شماره رسید",
            "انبار", "شماره صفحه", "عملیات"
        }, worksheet.Row(1).Cells(1, 15).Select(cell => cell.GetString()));
        Assert.Equal("کالای 501", worksheet.Cell(502, 3).GetString());
        Assert.Equal(501, worksheet.Cell(502, 1).GetValue<int>());
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(true, InvoiceStatus.DoesNotHave)]
    [InlineData(false, InvoiceStatus.HasInvoice)]
    public async Task Create_ForwardsNullableStatus_AndUsesNullWithoutInvoicePermission(bool invoicePermission, InvoiceStatus? status)
    {
        var sender = Substitute.For<ISender>();
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(invoicePermission ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        sender.Send(Arg.Any<CreateGoodsReceiptCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<GoodsReceiptSummary>.Success(new GoodsReceiptSummary()));
        var controller = new GoodsReceiptsController(sender, auth)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var response = await controller.Create(Request(status), default);

        Assert.IsType<CreatedAtActionResult>(response.Result);
        await sender.Received(1).Send(Arg.Is<CreateGoodsReceiptCommand>(command =>
            command.Items.Single().InvoiceStatus == (invoicePermission ? status : null)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Update_UsesServerPermissions(bool initial, bool invoice)
    {
        var sender = Substitute.For<ISender>();
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(call => (call.Arg<string>() == PermissionPolicyNames.Inventory.GoodsReceiptsInitialEntry ? initial :
                call.Arg<string>() == PermissionPolicyNames.Inventory.GoodsReceiptsReceiptInvoiceOperations && invoice)
                ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        sender.Send(Arg.Any<UpdateGoodsReceiptCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<GoodsReceiptSummary>.Success(new GoodsReceiptSummary()));
        var controller = new GoodsReceiptsController(sender, auth)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var id = Guid.NewGuid();
        var response = await controller.Update(id, Request(), default);
        if (!initial && !invoice)
        {
            Assert.IsType<ForbidResult>(response.Result);
            await sender.DidNotReceive().Send(Arg.Any<UpdateGoodsReceiptCommand>(), Arg.Any<CancellationToken>());
        }
        else
        {
            Assert.IsType<OkObjectResult>(response.Result);
            await sender.Received(1).Send(Arg.Is<UpdateGoodsReceiptCommand>(command =>
                command.Id == id && command.CanEditInitialEntry == initial && command.CanEditReceiptInvoice == invoice),
                Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task Update_NewMasterDataWithoutPermission_IsForbidden()
    {
        var sender = Substitute.For<ISender>();
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(call => call.Arg<string>() == PermissionPolicyNames.Inventory.GoodsReceiptsInitialEntry
                ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        var controller = new GoodsReceiptsController(sender, auth)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var response = await controller.Update(Guid.NewGuid(), Request() with { DelivererId = null, DelivererFullName = "جدید" }, default);
        Assert.IsType<ForbidResult>(response.Result);
        await sender.DidNotReceive().Send(Arg.Any<UpdateGoodsReceiptCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithConcurrencyConflict_ReturnsHttp409()
    {
        var sender = Substitute.For<ISender>();
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(AuthorizationResult.Success());
        sender.Send(Arg.Any<UpdateGoodsReceiptCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<GoodsReceiptSummary>.Error(ConcurrencyMessages.Conflict));
        var controller = new GoodsReceiptsController(sender, auth)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var response = await controller.Update(Guid.NewGuid(), Request(), default);

        Assert.IsType<ConflictObjectResult>(response.Result);
    }

    [Fact]
    public async Task Create_NewProductWithMasterDataManageButWithoutProductsCreate_IsForbidden()
    {
        var sender = Substitute.For<ISender>();
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(call => call.Arg<string>() == PermissionPolicyNames.MasterData.Manage
                ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        var controller = new GoodsReceiptsController(sender, auth)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var response = await controller.Create(RequestWithNewProduct(), default);

        Assert.IsType<ForbidResult>(response.Result);
        await sender.DidNotReceive().Send(Arg.Any<CreateGoodsReceiptCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_NewProductWithProductsCreate_IsAllowed()
    {
        var sender = Substitute.For<ISender>();
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(call => call.Arg<string>() == PermissionPolicyNames.MasterData.ProductsCreate
                ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        sender.Send(Arg.Any<CreateGoodsReceiptCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<GoodsReceiptSummary>.Success(new GoodsReceiptSummary()));
        var controller = new GoodsReceiptsController(sender, auth)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var response = await controller.Create(RequestWithNewProduct(), default);

        Assert.IsType<CreatedAtActionResult>(response.Result);
        await sender.Received(1).Send(Arg.Any<CreateGoodsReceiptCommand>(), Arg.Any<CancellationToken>());
    }

    private static CreateGoodsReceiptRequest Request(
        InvoiceStatus? status = InvoiceStatus.DoesNotHave) => new(
        DateTimeOffset.UtcNow, null, Guid.NewGuid(), null, "1",
        [new CreateGoodsReceiptItemRequest(
            Guid.NewGuid(), null, null, null, Guid.NewGuid(), status, null,
            null, null, null, 1, null, null)]);

    private static CreateGoodsReceiptRequest RequestWithNewProduct() => new(
        DateTimeOffset.UtcNow, null, Guid.NewGuid(), null, "1",
        [new CreateGoodsReceiptItemRequest(
            null, "کالای جدید", "NEW-1", Guid.NewGuid(), Guid.NewGuid(),
            InvoiceStatus.DoesNotHave, null, null, null, null, 1, null, null)]);

    private static GoodsReceiptItemRowSummary ExportRow(string productTitle) => new()
    {
        Id = Guid.NewGuid(),
        GoodsReceiptId = Guid.NewGuid(),
        ProductId = Guid.NewGuid(),
        ProductTitle = productTitle,
        ProductCode = "P-1",
        UnitOfMeasureTitle = "عدد",
        Quantity = 2.5m,
        ItemDescription = "توضیحات",
        EnteredAtUtc = new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.Zero),
        DelivererFullName = "تحویل دهنده",
        WarehouseTitle = "انبار اصلی",
        InvoiceStatus = InvoiceStatus.HasInvoice,
        InvoiceNumber = "I-1",
        Counterparty = "طرف حساب",
        ReceiptNumber = "R-1",
        PageNumber = "10"
    };
}
