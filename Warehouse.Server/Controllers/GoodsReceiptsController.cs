using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.ImportGoodsReceipts;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.UpdateGoodsReceipt;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.DeleteGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptById;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptItemRows;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptOptions;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceipts;
using Warehouse.Server.Contracts.Inventory;
using Warehouse.Domain.Enums;

namespace Warehouse.Server.Controllers;

[ApiController]
[Route("endpoint/inventory/goods-receipts")]
public sealed class GoodsReceiptsController(
    ISender sender,
    IAuthorizationService authorizationService) : ControllerBase
{
    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsReceiptsInitialEntry)]
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(51 * 1024 * 1024)]
    public async Task<ActionResult<Result<GoodsReceiptImportSummary>>> Import(
        [FromForm] ImportGoodsReceiptsRequest request, CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
            return BadRequest(Result<GoodsReceiptImportSummary>.Error("فایل Excel را انتخاب کنید."));
        if (request.File.Length > ImportGoodsReceiptsCommand.MaxFileSizeBytes)
            return BadRequest(Result<GoodsReceiptImportSummary>.Error("حجم فایل نباید بیشتر از ۵۰ مگابایت باشد."));
        var canManageInvoice = (await authorizationService.AuthorizeAsync(User,
            PermissionPolicyNames.Inventory.GoodsReceiptsReceiptInvoiceOperations)).Succeeded;
        await using var stream = new MemoryStream();
        await request.File.CopyToAsync(stream, cancellationToken);
        var result = await sender.Send(new ImportGoodsReceiptsCommand(request.File.FileName,
            stream.ToArray(), canManageInvoice), cancellationToken);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsReceiptsRead)]
    [HttpGet("options")]
    [ProducesResponseType(typeof(Result<GoodsReceiptOptions>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<GoodsReceiptOptions>>> GetOptions(
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetGoodsReceiptOptionsQuery(), cancellationToken));

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsReceiptsRead)]
    [HttpGet]
    [ProducesResponseType(typeof(Result<GoodsReceiptListResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<GoodsReceiptListResult>>> GetAll(
        [FromQuery] GetGoodsReceiptsQuery query,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(query, cancellationToken));

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsReceiptsRead)]
    [HttpGet("items")]
    [ProducesResponseType(typeof(Result<GoodsReceiptItemRowListResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<GoodsReceiptItemRowListResult>>> GetItems(
        [FromQuery] GetGoodsReceiptItemRowsQuery query,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(query, cancellationToken));

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsReceiptsRead)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Result<GoodsReceiptSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<GoodsReceiptSummary>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<GoodsReceiptSummary>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetGoodsReceiptByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsReceiptsInitialEntry)]
    [HttpPost]
    [ProducesResponseType(typeof(Result<GoodsReceiptSummary>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result<GoodsReceiptSummary>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Result<GoodsReceiptSummary>>> Create(
        [FromBody] CreateGoodsReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var canManageReceiptInvoice =
            (await authorizationService.AuthorizeAsync(
                User,
                PermissionPolicyNames.Inventory.GoodsReceiptsReceiptInvoiceOperations)).Succeeded;
        var createsMasterData =
            (!request.DelivererId.HasValue && !string.IsNullOrWhiteSpace(request.DelivererFullName)) ||
            (canManageReceiptInvoice &&
             !request.SupplierId.HasValue &&
             !string.IsNullOrWhiteSpace(request.SupplierTitle)) ||
            request.Items.Any(item => !item.ProductId.HasValue);
        if (createsMasterData &&
            !(await authorizationService.AuthorizeAsync(
                User,
                PermissionPolicyNames.MasterData.Manage)).Succeeded)
        {
            return Forbid();
        }

        var command = new CreateGoodsReceiptCommand(
            request.EnteredAtUtc,
            request.Description,
            request.DelivererId,
            request.DelivererFullName,
            request.WarehouseId,
            canManageReceiptInvoice ? request.InvoiceStatus : null,
            canManageReceiptInvoice ? request.InvoiceNumber : null,
            canManageReceiptInvoice ? request.SupplierId : null,
            canManageReceiptInvoice ? request.SupplierTitle : null,
            canManageReceiptInvoice ? request.ReceiptNumber : null,
            request.PageNumber,
            request.Items.Select(item => new CreateGoodsReceiptItem(
                item.ProductId,
                item.ProductTitle,
                item.ProductCode,
                item.UnitOfMeasureId,
                item.Quantity,
                item.RequestNumber,
                item.Description)).ToArray());
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result)
            : BadRequest(result);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result<GoodsReceiptSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<GoodsReceiptSummary>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Result<GoodsReceiptSummary>>> Update(
        Guid id,
        [FromBody] CreateGoodsReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var canManageReceiptInvoice =
            (await authorizationService.AuthorizeAsync(
                User,
                PermissionPolicyNames.Inventory.GoodsReceiptsReceiptInvoiceOperations)).Succeeded;
        var canInitialEntry = (await authorizationService.AuthorizeAsync(
            User, PermissionPolicyNames.Inventory.GoodsReceiptsInitialEntry)).Succeeded;
        if (!canInitialEntry && !canManageReceiptInvoice) return Forbid();
        var createsMasterData =
            (canInitialEntry && !request.DelivererId.HasValue && !string.IsNullOrWhiteSpace(request.DelivererFullName)) ||
            (canManageReceiptInvoice &&
             !request.SupplierId.HasValue &&
             !string.IsNullOrWhiteSpace(request.SupplierTitle)) ||
            (canInitialEntry && request.Items.Any(item => !item.ProductId.HasValue));
        if (createsMasterData &&
            !(await authorizationService.AuthorizeAsync(
                User,
                PermissionPolicyNames.MasterData.Manage)).Succeeded)
        {
            return Forbid();
        }

        var command = new CreateGoodsReceiptCommand(
            request.EnteredAtUtc,
            request.Description,
            request.DelivererId,
            request.DelivererFullName,
            request.WarehouseId,
            canManageReceiptInvoice ? request.InvoiceStatus : null,
            canManageReceiptInvoice ? request.InvoiceNumber : null,
            canManageReceiptInvoice ? request.SupplierId : null,
            canManageReceiptInvoice ? request.SupplierTitle : null,
            canManageReceiptInvoice ? request.ReceiptNumber : null,
            request.PageNumber,
            request.Items.Select(item => new CreateGoodsReceiptItem(
                item.ProductId,
                item.ProductTitle,
                item.ProductCode,
                item.UnitOfMeasureId,
                item.Quantity,
                item.RequestNumber,
                item.Description)).ToArray());
        var result = await sender.Send(new UpdateGoodsReceiptCommand(id, command, canInitialEntry, canManageReceiptInvoice), cancellationToken);
        return result.IsSuccess
            ? Ok(result)
            : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsReceiptsInitialEntry)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteGoodsReceiptCommand(id), cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }
}
