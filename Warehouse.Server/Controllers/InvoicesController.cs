using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.Invoices.Commands.UpdateInvoiceItem;
using Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoiceById;
using Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoices;
using Warehouse.Server.Contracts.Inventory;

namespace Warehouse.Server.Controllers;

[ApiController]
[Route("endpoint/inventory/invoices")]
[Authorize(Policy = PermissionPolicyNames.Inventory.InvoicesRead)]
public sealed class InvoicesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Result<InvoiceListResult>>> GetAll(
        [FromQuery] GetInvoicesQuery query,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(query, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Result<InvoiceDetails>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetInvoiceByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
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
