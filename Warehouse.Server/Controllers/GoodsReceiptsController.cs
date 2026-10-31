using System.Globalization;
using ClosedXML.Excel;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.ImportGoodsReceipts;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.UpdateGoodsReceipt;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.DeleteGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptById;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptItemRows;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptMetrics;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptOptions;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceipts;
using Warehouse.Application.Features.Auditing.Queries.GetGoodsReceiptItemHistory;
using Warehouse.Server.Contracts.Inventory;
using Warehouse.Domain.Enums;

namespace Warehouse.Server.Controllers;

[ApiController]
[TypeFilter(typeof(Warehouse.Server.Filters.GoodsReceiptColumnAccessFilter))]
[Route("endpoint/inventory/goods-receipts")]
public sealed class GoodsReceiptsController(
    ISender sender,
    IAuthorizationService authorizationService) : ControllerBase
{
    private const int ExportBatchSize = 500;

    private async Task<bool> CanManageInvoiceAsync() =>
        (await authorizationService.AuthorizeAsync(User, PermissionPolicyNames.Inventory.GoodsReceiptsReceiptInvoiceOperations)).Succeeded &&
        (await authorizationService.AuthorizeAsync(User, PermissionPolicyNames.Inventory.GoodsReceiptsInvoiceStatusRead)).Succeeded &&
        (await authorizationService.AuthorizeAsync(User, PermissionPolicyNames.Inventory.GoodsReceiptsInvoiceNumberRead)).Succeeded &&
        (await authorizationService.AuthorizeAsync(User, PermissionPolicyNames.Inventory.GoodsReceiptsCounterpartyRead)).Succeeded;

    private async Task<bool> CanManageReceiptAsync() =>
        (await authorizationService.AuthorizeAsync(User, PermissionPolicyNames.Inventory.GoodsReceiptsReceiptRegistration)).Succeeded &&
        (await authorizationService.AuthorizeAsync(User, PermissionPolicyNames.Inventory.GoodsReceiptsReceiptNumberRead)).Succeeded;

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsReceiptsImport)]
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
        var canManageInvoice = await CanManageInvoiceAsync();
        var canManageReceipt = await CanManageReceiptAsync();
        await using var stream = new MemoryStream();
        await request.File.CopyToAsync(stream, cancellationToken);
        var result = await sender.Send(new ImportGoodsReceiptsCommand(request.File.FileName,
            stream.ToArray(), canManageInvoice, canManageReceipt), cancellationToken);
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
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(Result<GoodsReceiptMetrics>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<GoodsReceiptMetrics>>> GetMetrics(
        CancellationToken cancellationToken)
    {
        async Task<bool> CanRead(string policy) =>
            (await authorizationService.AuthorizeAsync(User, policy)).Succeeded;

        var query = new GetGoodsReceiptMetricsQuery(
            await CanRead(PermissionPolicyNames.Inventory.GoodsReceiptsMissingReceiptCountRead),
            await CanRead(PermissionPolicyNames.Inventory.GoodsReceiptsMissingInvoiceCountRead),
            await CanRead(PermissionPolicyNames.Inventory.GoodsReceiptsTodayItemCountRead),
            await CanRead(PermissionPolicyNames.Inventory.GoodsReceiptsTodayDocumentCountRead),
            DateTimeOffset.UtcNow);
        return Ok(await sender.Send(query, cancellationToken));
    }

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsReceiptHistoryRead)]
    [HttpGet("items/{itemId:guid}/history")]
    [ProducesResponseType(typeof(Result<Warehouse.Application.Common.Models.Auditing.GoodsReceiptItemHistoryResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<Warehouse.Application.Common.Models.Auditing.GoodsReceiptItemHistoryResult>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<Warehouse.Application.Common.Models.Auditing.GoodsReceiptItemHistoryResult>>> GetItemHistory(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetGoodsReceiptItemHistoryQuery(itemId), cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }


    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsReceiptsExport)]
    [HttpGet("items/export")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task<IActionResult> ExportItems(
        [FromQuery] GetGoodsReceiptItemRowsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await Warehouse.Server.Filters.GoodsReceiptColumnAccessFilter.GetAccessAsync(authorizationService, User);
        query = query with { ColumnAccess = access };
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("ورودی کالا");
        worksheet.RightToLeft = true;

        WriteExportHeader(worksheet);

        var excelRow = 2;
        var pageNumber = 1;
        while (true)
        {
            var batchResult = await sender.Send(query with
            {
                PageNumber = pageNumber,
                PageSize = ExportBatchSize
            }, cancellationToken);

            if (!batchResult.IsSuccess || batchResult.Data is null)
                return BadRequest(batchResult);

            var batch = batchResult.Data.Items;
            foreach (var item in batch)
            {
                WriteExportRow(worksheet, excelRow, excelRow - 1, item);
                excelRow++;
            }

            if (batch.Count < ExportBatchSize || excelRow - 2 >= batchResult.Data.FilteredCount)
                break;

            pageNumber++;
        }

        FormatExportWorksheet(worksheet, excelRow - 1);
        foreach (var (column, allowed) in new[]
        {
            (13, access.Warehouse), (12, access.ReceiptNumber), (11, access.Counterparty),
            (10, access.InvoiceNumber), (9, access.InvoiceStatus)
        })
        {
            if (!allowed) worksheet.Column(column).Delete();
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var fileName = $"goods-receipts-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
        return File(
            stream,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

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

    private static void WriteExportHeader(IXLWorksheet worksheet)
    {
        string[] headers =
        [
            "ردیف",
            "تاریخ",
            "عنوان کالا",
            "کد کالا",
            "تعداد",
            "واحد",
            "توضیحات",
            "تحویل دهنده",
            "فاکتور",
            "شماره فاکتور",
            "طرف حساب",
            "شماره رسید",
            "انبار",
            "شماره صفحه",
            "عملیات"
        ];

        for (var column = 1; column <= headers.Length; column++)
            worksheet.Cell(1, column).Value = headers[column - 1];
    }

    private static void WriteExportRow(
        IXLWorksheet worksheet,
        int row,
        int rowNumber,
        GoodsReceiptItemRowSummary item)
    {
        worksheet.Cell(row, 1).Value = rowNumber;
        worksheet.Cell(row, 2).Value = FormatPersianDate(item.EnteredAtUtc);
        worksheet.Cell(row, 3).Value = item.ProductTitle;
        worksheet.Cell(row, 4).Value = item.ProductCode;
        worksheet.Cell(row, 5).Value = item.Quantity;
        worksheet.Cell(row, 6).Value = item.UnitOfMeasureTitle;
        worksheet.Cell(row, 7).Value = item.ItemDescription ?? string.Empty;
        worksheet.Cell(row, 8).Value = item.DelivererFullName;
        worksheet.Cell(row, 9).Value = item.InvoiceStatus switch
        {
            InvoiceStatus.HasInvoice => "دارد",
            InvoiceStatus.DoesNotHave => "ندارد",
            _ => "وارد نشده"
        };
        worksheet.Cell(row, 10).Value = item.InvoiceNumber ?? string.Empty;
        worksheet.Cell(row, 11).Value = item.Counterparty ?? string.Empty;
        worksheet.Cell(row, 12).Value = item.ReceiptNumber ?? string.Empty;
        worksheet.Cell(row, 13).Value = item.WarehouseTitle;
        worksheet.Cell(row, 14).Value = item.PageNumber;
        worksheet.Cell(row, 15).Value = string.Empty;
    }

    private static string FormatPersianDate(DateTimeOffset value)
    {
        var local = value.ToLocalTime();
        var calendar = new PersianCalendar();
        return $"{calendar.GetYear(local.DateTime):0000}/" +
               $"{calendar.GetMonth(local.DateTime):00}/" +
               $"{calendar.GetDayOfMonth(local.DateTime):00}";
    }

    private static void FormatExportWorksheet(IXLWorksheet worksheet, int lastRow)
    {
        var header = worksheet.Range(1, 1, 1, 15);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        if (lastRow > 1)
        {
            var data = worksheet.Range(2, 1, lastRow, 15);
            data.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            data.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            data.Style.Border.OutsideBorder = XLBorderStyleValues.Thick;
            worksheet.Rows().Height = 17;
            //  data.Style.Border.BottomBorderColor = XLColor.FromHtml("#CBD5E1");
            worksheet.Range(1, 1, lastRow, 15).SetAutoFilter();
            worksheet.Range(2, 1, lastRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Range(2, 2, lastRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Range(2, 4, lastRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Range(2, 9, lastRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Range(2, 14, lastRow, 14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Range(2, 5, lastRow, 5).Style.NumberFormat.Format = "#,##0.###";
        }

        worksheet.SheetView.FreezeRows(1);
        worksheet.Column(1).Width = 8;
        worksheet.Column(2).Width = 16;
        worksheet.Column(3).Width = 14;
        worksheet.Column(4).Width = 28;
        worksheet.Column(5).Width = 16;
        worksheet.Column(6).Width = 12;
        worksheet.Column(7).Width = 14;
        worksheet.Column(8).Width = 35;
        worksheet.Column(9).Width = 22;
        worksheet.Column(10).Width = 14;
        worksheet.Column(11).Width = 18;
        worksheet.Column(12).Width = 24;
        worksheet.Column(13).Width = 18;
        worksheet.Column(14).Width = 16;
        worksheet.Column(15).Width = 12;
        worksheet.Column(7).Style.Alignment.WrapText = true;
        worksheet.Row(1).Height = 24;
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
        var canManageInvoice = await CanManageInvoiceAsync();
        var canManageReceipt = await CanManageReceiptAsync();
        var createsOtherMasterData =
            (!request.DelivererId.HasValue && !string.IsNullOrWhiteSpace(request.DelivererFullName)) ||
            (canManageInvoice && request.Items.Any(item =>
                !item.SupplierId.HasValue && !string.IsNullOrWhiteSpace(item.SupplierTitle)));
        if (createsOtherMasterData &&
            !(await authorizationService.AuthorizeAsync(
                User,
                PermissionPolicyNames.MasterData.Manage)).Succeeded)
        {
            return Forbid();
        }
        if (request.Items.Any(item => !item.ProductId.HasValue) &&
            !(await authorizationService.AuthorizeAsync(
                User,
                PermissionPolicyNames.MasterData.ProductsCreate)).Succeeded)
        {
            return Forbid();
        }

        var command = new CreateGoodsReceiptCommand(
            request.EnteredAtUtc,
            request.Description,
            request.DelivererId,
            request.DelivererFullName,
            request.PageNumber,
            request.Items.Select(item => new CreateGoodsReceiptItem(
                item.ProductId,
                item.ProductTitle,
                item.ProductCode,
                item.UnitOfMeasureId,
                item.WarehouseId,
                canManageInvoice ? item.InvoiceStatus : null,
                canManageInvoice ? item.InvoiceNumber : null,
                canManageInvoice ? item.SupplierId : null,
                canManageInvoice ? item.SupplierTitle : null,
                canManageReceipt ? item.ReceiptNumber : null,
                item.Quantity,
                item.RequestNumber,
                item.Description,
                item.IssueNumber)).ToArray());
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result)
            : BadRequest(result);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result<GoodsReceiptSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<GoodsReceiptSummary>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<GoodsReceiptSummary>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Result<GoodsReceiptSummary>>> Update(
        Guid id,
        [FromBody] CreateGoodsReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var canManageInvoice = await CanManageInvoiceAsync();
        var canManageReceipt = await CanManageReceiptAsync();
        var canInitialEntry = (await authorizationService.AuthorizeAsync(
            User, PermissionPolicyNames.Inventory.GoodsReceiptsInitialEntry)).Succeeded;
        var canEditWarehouse =
            (await authorizationService.AuthorizeAsync(User, PermissionPolicyNames.Inventory.GoodsReceiptsWarehouseUpdate)).Succeeded &&
            (await authorizationService.AuthorizeAsync(User, PermissionPolicyNames.Inventory.GoodsReceiptsWarehouseRead)).Succeeded;
        if (!canInitialEntry && !canManageInvoice && !canManageReceipt && !canEditWarehouse) return Forbid();
        var createsOtherMasterData =
            (canInitialEntry && !request.DelivererId.HasValue && !string.IsNullOrWhiteSpace(request.DelivererFullName)) ||
            (canManageInvoice && request.Items.Any(item =>
                !item.SupplierId.HasValue && !string.IsNullOrWhiteSpace(item.SupplierTitle)));
        if (createsOtherMasterData &&
            !(await authorizationService.AuthorizeAsync(
                User,
                PermissionPolicyNames.MasterData.Manage)).Succeeded)
        {
            return Forbid();
        }
        if (canInitialEntry && request.Items.Any(item => !item.ProductId.HasValue) &&
            !(await authorizationService.AuthorizeAsync(
                User,
                PermissionPolicyNames.MasterData.ProductsCreate)).Succeeded)
        {
            return Forbid();
        }

        var command = new CreateGoodsReceiptCommand(
            request.EnteredAtUtc,
            request.Description,
            request.DelivererId,
            request.DelivererFullName,
            request.PageNumber,
            request.Items.Select(item => new CreateGoodsReceiptItem(
                item.ProductId,
                item.ProductTitle,
                item.ProductCode,
                item.UnitOfMeasureId,
                item.WarehouseId,
                canManageInvoice ? item.InvoiceStatus : null,
                canManageInvoice ? item.InvoiceNumber : null,
                canManageInvoice ? item.SupplierId : null,
                canManageInvoice ? item.SupplierTitle : null,
                canManageReceipt ? item.ReceiptNumber : null,
                item.Quantity,
                item.RequestNumber,
                item.Description,
                canInitialEntry ? item.IssueNumber : null)).ToArray());
        var result = await sender.Send(new UpdateGoodsReceiptCommand(
            id, command, canInitialEntry, canManageInvoice, canManageReceipt,
            request.ConcurrencyStamp ?? Guid.Empty, canEditWarehouse), cancellationToken);
        return result.IsSuccess
            ? Ok(result)
            : result.Messages.Contains(ConcurrencyMessages.Conflict, StringComparer.Ordinal)
                ? Conflict(result)
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
