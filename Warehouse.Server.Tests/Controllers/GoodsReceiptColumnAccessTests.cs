using System.Security.Claims;
using ClosedXML.Excel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Auditing;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.UpdateGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptItemRows;
using Warehouse.Domain.Enums;
using Warehouse.Server.Contracts.Inventory;
using Warehouse.Server.Controllers;
using Warehouse.Server.Filters;

namespace Warehouse.Server.Tests.Controllers;

public sealed class GoodsReceiptColumnAccessTests
{
    public static TheoryData<string, string> Columns => new()
    {
        { PermissionPolicyNames.Inventory.GoodsReceiptsWarehouseRead, "انبار" },
        { PermissionPolicyNames.Inventory.GoodsReceiptsInvoiceStatusRead, "فاکتور" },
        { PermissionPolicyNames.Inventory.GoodsReceiptsCounterpartyRead, "طرف حساب" },
        { PermissionPolicyNames.Inventory.GoodsReceiptsInvoiceNumberRead, "شماره فاکتور" },
        { PermissionPolicyNames.Inventory.GoodsReceiptsReceiptNumberRead, "شماره رسید" }
    };

    [Theory]
    [MemberData(nameof(Columns))]
    public async Task Filter_UsesServerPermissionsForEachColumn(string permission, string _)
    {
        var auth = Authorization(policy => policy == permission);
        var row = new GoodsReceiptItemRowSummary
        {
            ProductTitle = "کالا", WarehouseTitle = "انبار محرمانه", InvoiceStatus = InvoiceStatus.HasInvoice,
            Counterparty = "طرف حساب محرمانه", SupplierId = Guid.NewGuid(),
            InvoiceNumber = "INV-SECRET", ReceiptNumber = "REC-SECRET"
        };
        var query = new GetGoodsReceiptItemRowsQuery { ColumnAccess = GoodsReceiptColumnAccess.All };
        var args = new Dictionary<string, object?> { ["query"] = query };
        var result = new OkObjectResult(Result<GoodsReceiptItemRowListResult>.Success(new([row], 1, 1, 1, 20)));
        await ApplyFilter(auth, result, args);
        var access = ((GetGoodsReceiptItemRowsQuery)args["query"]!).ColumnAccess;
        var visible = Assert.Single(Assert.IsType<Result<GoodsReceiptItemRowListResult>>(result.Value).Data!.Items);
        Assert.Equal("کالا", visible.ProductTitle);
        Assert.Equal(access.Warehouse ? row.WarehouseTitle : "", visible.WarehouseTitle);
        Assert.Equal(access.InvoiceStatus ? row.InvoiceStatus : null, visible.InvoiceStatus);
        Assert.Equal(access.Counterparty ? row.Counterparty : null, visible.Counterparty);
        Assert.Equal(access.Counterparty ? row.SupplierId : null, visible.SupplierId);
        Assert.Equal(access.InvoiceNumber ? row.InvoiceNumber : null, visible.InvoiceNumber);
        Assert.Equal(access.ReceiptNumber ? row.ReceiptNumber : null, visible.ReceiptNumber);
        Assert.Equal(1, new[] { access.Warehouse, access.InvoiceStatus, access.Counterparty, access.InvoiceNumber, access.ReceiptNumber }.Count(value => value));
    }

    [Fact]
    public async Task Filter_MasksDetailsOptionsAndHistoryWithoutChangingOriginalData()
    {
        var auth = Authorization(_ => false);
        var item = new GoodsReceiptItemSummary
        {
            WarehouseId = Guid.NewGuid(), WarehouseTitle = "انبار", InvoiceStatus = InvoiceStatus.HasInvoice,
            SupplierId = Guid.NewGuid(), Counterparty = "طرف حساب", InvoiceNumber = "INV-1", ReceiptNumber = "R-1"
        };
        var result = new OkObjectResult(Result<GoodsReceiptSummary>.Success(new() { Items = [item] }));
        await ApplyFilter(auth, result, []);
        var masked = Assert.Single(Assert.IsType<Result<GoodsReceiptSummary>>(result.Value).Data!.Items);
        Assert.Equal(Guid.Empty, masked.WarehouseId);
        Assert.Empty(masked.WarehouseTitle);
        Assert.Null(masked.InvoiceStatus);
        Assert.Null(masked.SupplierId);
        Assert.Null(masked.Counterparty);
        Assert.Null(masked.InvoiceNumber);
        Assert.Null(masked.ReceiptNumber);
        Assert.NotEqual(Guid.Empty, item.WarehouseId);
        Assert.Equal("INV-1", item.InvoiceNumber);

        var options = new OkObjectResult(Result<GoodsReceiptOptions>.Success(new() { Warehouses = [new() { Id = Guid.NewGuid(), Title = "انبار" }] }));
        await ApplyFilter(auth, options, []);
        Assert.Empty(Assert.IsType<Result<GoodsReceiptOptions>>(options.Value).Data!.Warehouses);

        var history = new OkObjectResult(Result<GoodsReceiptItemHistoryResult>.Success(new(Guid.NewGuid(), "کالا",
            [new(1, "Update", "ویرایش", "ورود", "کاربر", DateTimeOffset.UtcNow,
                [new("WarehouseId", "انبار", "A", "B"), new("InvoiceId", "فاکتور", "I1 - S1", "I2 - S2"), new("Quantity", "مقدار", "1", "2")])])));
        await ApplyFilter(auth, history, []);
        var changes = Assert.Single(Assert.IsType<Result<GoodsReceiptItemHistoryResult>>(history.Value).Data!.Entries).Changes;
        Assert.Equal("Quantity", Assert.Single(changes).FieldName);
    }

    [Theory]
    [MemberData(nameof(Columns))]
    public async Task Export_ContainsOnlyAuthorizedSensitiveColumn(string permission, string header)
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetGoodsReceiptItemRowsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<GoodsReceiptItemRowListResult>.Success(new([], 0, 0, 1, 500)));
        var controller = new GoodsReceiptsController(sender, Authorization(policy => policy == permission))
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var response = await controller.ExportItems(new(), default);
        using var workbook = new XLWorkbook(Assert.IsType<FileStreamResult>(response).FileStream);
        var headers = workbook.Worksheet(1).Row(1).CellsUsed().Select(cell => cell.GetString()).ToArray();
        foreach (var column in Columns)
            Assert.Equal((string)column[1] == header, headers.Contains((string)column[1]));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task UpdateWarehouse_RequiresEditAndReadPermissions(bool edit, bool read)
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<UpdateGoodsReceiptCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<GoodsReceiptSummary>.Success(new()));
        var controller = new GoodsReceiptsController(sender, Authorization(policy =>
            policy == PermissionPolicyNames.Inventory.GoodsReceiptsWarehouseUpdate && edit ||
            policy == PermissionPolicyNames.Inventory.GoodsReceiptsWarehouseRead && read))
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var request = new CreateGoodsReceiptRequest(DateTimeOffset.UtcNow, null, Guid.NewGuid(), null, "1", []);
        var response = await controller.Update(Guid.NewGuid(), request, default);
        if (edit && read)
        {
            Assert.IsType<OkObjectResult>(response.Result);
            await sender.Received().Send(Arg.Is<UpdateGoodsReceiptCommand>(command => command.CanEditWarehouse && !command.CanEditInitialEntry), Arg.Any<CancellationToken>());
        }
        else
        {
            Assert.IsType<ForbidResult>(response.Result);
            await sender.DidNotReceive().Send(Arg.Any<UpdateGoodsReceiptCommand>(), Arg.Any<CancellationToken>());
        }
    }

    private static IAuthorizationService Authorization(Func<string, bool> allowed)
    {
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(call => allowed(call.ArgAt<string>(2)) ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        return auth;
    }

    private static async Task ApplyFilter(IAuthorizationService auth, ObjectResult result, Dictionary<string, object?> args)
    {
        var action = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var controller = new object();
        var executing = new ActionExecutingContext(action, [], args, controller);
        await new GoodsReceiptColumnAccessFilter(auth).OnActionExecutionAsync(executing, () => Task.FromResult(
            new ActionExecutedContext(action, [], controller) { Result = result }));
    }
}
