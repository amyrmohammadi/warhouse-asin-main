using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.UpdateGoodsReceipt;
using Warehouse.Domain.Enums;
using Warehouse.Server.Contracts.Inventory;
using Warehouse.Server.Controllers;

namespace Warehouse.Server.Tests.Controllers;

public sealed class GoodsReceiptsControllerTests
{
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

        var response = await controller.Create(Request() with { InvoiceStatus = status }, default);

        Assert.IsType<CreatedAtActionResult>(response.Result);
        await sender.Received(1).Send(Arg.Is<CreateGoodsReceiptCommand>(command =>
            command.InvoiceStatus == (invoicePermission ? status : null)), Arg.Any<CancellationToken>());
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

    private static CreateGoodsReceiptRequest Request() => new(DateTimeOffset.UtcNow, null,
        Guid.NewGuid(), null, Guid.NewGuid(), InvoiceStatus.DoesNotHave, null, null, null, null, "1",
        [new CreateGoodsReceiptItemRequest(Guid.NewGuid(), null, null, null, 1, null, null)]);
}
