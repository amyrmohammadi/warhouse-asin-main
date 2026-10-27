using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.Invoices.Commands.UpdateInvoiceItem;
using Warehouse.Application.Features.Inventory.Invoices.Commands.UpdateInvoice;
using Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoiceById;
using Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoices;
using Warehouse.Server.Contracts.Inventory;

namespace Warehouse.Server.Controllers;

[ApiController]
[Route("endpoint/inventory/invoices")]
[Authorize(Policy = PermissionPolicyNames.Inventory.InvoicesRead)]
public sealed class InvoicesController(
    ISender sender,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Result<InvoiceListResult>>> GetAll(
        [FromQuery] GetInvoicesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(query, cancellationToken);
        if (!result.IsSuccess || result.Data is null ||
            (await authorizationService.AuthorizeAsync(
                User,
                PermissionPolicyNames.Inventory.InvoicesMissingReceiptCountRead)).Succeeded)
        {
            return Ok(result);
        }

        var redactedItems = result.Data.Items.Select(item => new InvoiceSummary
        {
            Id = item.Id,
            InvoiceNumber = item.InvoiceNumber,
            Description = item.Description,
            InvoiceDate = item.InvoiceDate,
            ProcessingStatus = item.ProcessingStatus,
            Counterparty = item.Counterparty,
            ReceiptNumbers = item.ReceiptNumbers
        }).ToArray();
        return Ok(Result<InvoiceListResult>.Success(
            result.Data with { Items = redactedItems },
            result.Messages.ToArray()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Result<InvoiceDetails>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetInvoiceByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsReceiptsReceiptInvoiceOperations)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result<InvoiceDetails>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<InvoiceDetails>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Result<InvoiceDetails>>> Update(
        Guid id,
        [FromBody] UpdateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateInvoiceCommand(id, request.InvoiceNumber,
            request.SupplierId, request.Description,
            request.InvoiceDate, request.ProcessingStatus,
            (request.Items ?? []).Select(item => new UpdateInvoiceReceiptItem(item.Id,
                item.ReceiptNumber, item.GoodsReceiptConcurrencyStamp)).ToArray()), cancellationToken);
        if (result.IsSuccess) return Ok(result);
        return result.Messages.Contains(ConcurrencyMessages.Conflict, StringComparer.Ordinal)
            ? Conflict(result)
            : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsReceiptsReceiptInvoiceOperations)]
    [HttpPut("{invoiceId:guid}/items/{itemId:guid}")]
    [ProducesResponseType(typeof(Result<UpdateInvoiceItemResult>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Result<UpdateInvoiceItemResult>>> UpdateItem(
        Guid invoiceId,
        Guid itemId,
        [FromBody] UpdateInvoiceItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateInvoiceItemCommand(
            invoiceId,
            itemId,
            request.ReceiptNumber,
            request.ConcurrencyStamp), cancellationToken);
        if (result.IsSuccess) return Ok(result);
        return result.Messages.Contains(ConcurrencyMessages.Conflict, StringComparer.Ordinal)
            ? Conflict(result)
            : NotFound(result);
    }
}
