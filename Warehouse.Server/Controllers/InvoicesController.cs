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
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Features.Inventory.Invoices.Commands.PrintInvoices;
using Warehouse.Application.Features.Inventory.Invoices.Commands.ChangeInvoiceStatus;
using Warehouse.Domain.Enums;

namespace Warehouse.Server.Controllers;

[ApiController]
[Route("endpoint/inventory/invoices")]
[Authorize(Policy = PermissionPolicyNames.Inventory.InvoicesRead)]
public sealed class InvoicesController(
    ISender sender,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<Result<InvoiceDetails>>> ChangeStatus(Guid id,
        [FromBody] ChangeInvoiceStatusRequest request, CancellationToken cancellationToken)
    {
        var reopen = request.Status == InvoiceProcessingStatus.UnderReview;
        if (request.Status != InvoiceProcessingStatus.UnderReview && request.Status != InvoiceProcessingStatus.FinanceApproved)
            return BadRequest(Result<InvoiceDetails>.Error("این تغییر وضعیت مجاز نیست."));
        var allowed = (await authorizationService.AuthorizeAsync(User, reopen
            ? PermissionPolicyNames.Inventory.InvoicesReopen
            : PermissionPolicyNames.Inventory.GoodsReceiptsReceiptInvoiceOperations)).Succeeded;
        if (!allowed) return Forbid();
        var result = await sender.Send(new ChangeInvoiceStatusCommand(id, request.Status,
            request.ExpectedStatus, CanReopen: reopen, CanApprove: !reopen), cancellationToken);
        if (result.IsSuccess) return Ok(result);
        return result.Messages.Contains(ConcurrencyMessages.Conflict, StringComparer.Ordinal)
            ? Conflict(result) : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.Inventory.InvoicesPrint)]
    [HttpPost("print")]
    public async Task<IActionResult> Print([FromBody] PrintInvoicesRequest request, CancellationToken cancellationToken)
    {
        if (request.SendToFinance && !(await authorizationService.AuthorizeAsync(User,
                PermissionPolicyNames.Inventory.GoodsReceiptsReceiptInvoiceOperations)).Succeeded)
            return Forbid();
        try
        {
            var result = await sender.Send(new PrintInvoicesCommand(request.InvoiceIds, request.SendToFinance), cancellationToken);
            if (!result.IsSuccess || result.Data is null) return BadRequest(result);
            Response.Headers.CacheControl = "no-store";
            return File(result.Data, "application/pdf");
        }
        catch (TimeoutException)
        {
            return Problem(statusCode: StatusCodes.Status504GatewayTimeout,
                title: "آماده‌سازی چاپ بیش از زمان مجاز طول کشید. وضعیت فاکتورها تغییر نکرد؛ دوباره تلاش کنید.");
        }
        catch (ExcelToPdfConversionException)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "تولید فایل چاپ انجام نشد و وضعیت فاکتورها تغییر نکرد. سرویس چاپ را بررسی کنید.");
        }
    }

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
            Price = item.Price,
            InvoiceNumber = item.InvoiceNumber,
            Description = item.Description,
            InvoiceDate = item.InvoiceDate,
            FinanceDeliveryDate = item.FinanceDeliveryDate,
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

    [Authorize]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result<InvoiceDetails>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<InvoiceDetails>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Result<InvoiceDetails>>> Update(
        Guid id,
        [FromBody] UpdateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var canEditInvoice = (await authorizationService.AuthorizeAsync(User,
            PermissionPolicyNames.Inventory.GoodsReceiptsReceiptInvoiceOperations)).Succeeded;
        var canEditReceipt = (await authorizationService.AuthorizeAsync(User,
            PermissionPolicyNames.Inventory.GoodsReceiptsReceiptRegistration)).Succeeded;
        if (!canEditInvoice && !canEditReceipt) return Forbid();

        var result = await sender.Send(new UpdateInvoiceCommand(id, request.InvoiceNumber,
            request.SupplierId, request.Description,
            request.InvoiceDate, request.ProcessingStatus,
            (request.Items ?? []).Select(item => new UpdateInvoiceReceiptItem(item.Id,
                item.ReceiptNumber, item.GoodsReceiptConcurrencyStamp, item.Price)).ToArray(),
            canEditInvoice, canEditReceipt, request.Price, request.FinanceDeliveryDate), cancellationToken);
        if (result.IsSuccess) return Ok(result);
        return result.Messages.Contains(ConcurrencyMessages.Conflict, StringComparer.Ordinal)
            ? Conflict(result)
            : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsReceiptsReceiptRegistration)]
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
