using System.Globalization;
using ClosedXML.Excel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsIssues.Commands.DeleteGoodsIssue;
using Warehouse.Application.Features.Inventory.GoodsIssues.Commands.ImportGoodsIssues;
using Warehouse.Application.Features.Inventory.GoodsIssues.Commands.SaveGoodsIssue;
using Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssueById;
using Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssueItems;
using Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssueOptions;
using Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssues;
using Warehouse.Domain.Enums;
using Warehouse.Server.Contracts.Inventory;

namespace Warehouse.Server.Controllers;

[ApiController]
[Route("endpoint/inventory/goods-issues")]
public sealed class GoodsIssuesController(
    ISender sender,
    IAuthorizationService authorizationService) : ControllerBase
{
    private const int ExportBatchSize = 100;

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsIssuesImport)]
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(51 * 1024 * 1024)]
    public async Task<ActionResult<Result<GoodsIssueImportSummary>>> Import(
        [FromForm] ImportGoodsIssuesRequest request, CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
            return BadRequest(Result<GoodsIssueImportSummary>.Error("فایل Excel را انتخاب کنید."));
        if (request.File.Length > ImportGoodsIssuesCommand.MaxFileSizeBytes)
            return BadRequest(Result<GoodsIssueImportSummary>.Error("حجم فایل نباید بیشتر از ۵۰ مگابایت باشد."));
        await using var stream = new MemoryStream();
        await request.File.CopyToAsync(stream, cancellationToken);
        var result = await sender.Send(new ImportGoodsIssuesCommand(request.File.FileName, stream.ToArray()), cancellationToken);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsIssuesRead)]
    [HttpGet]
    public async Task<ActionResult<Result<GoodsIssueListResult>>> GetAll(
        [FromQuery] GetGoodsIssuesQuery query,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(query, cancellationToken));

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsIssuesRead)]
    [HttpGet("items")]
    public async Task<ActionResult<Result<GoodsIssueItemRowListResult>>> GetItems(
        [FromQuery] GetGoodsIssueItemsQuery query,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(query, cancellationToken));

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsIssuesRead)]
    [HttpGet("options")]
    public async Task<ActionResult<Result<GoodsIssueOptions>>> GetOptions(
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetGoodsIssueOptionsQuery(), cancellationToken));

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsIssuesRead)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Result<GoodsIssueSummary>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetGoodsIssueByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsIssuesCreate)]
    [HttpPost]
    public async Task<ActionResult<Result<GoodsIssueSummary>>> Create(
        [FromBody] SaveGoodsIssueRequest request,
        CancellationToken cancellationToken)
    {
        if (CreatesMasterData(request) && !await CanManageMasterDataAsync()) return Forbid();
        if (ReferencesNewProduct(request) && !await CanCreateProductsAsync()) return Forbid();
        var result = await sender.Send(ToCommand(null, request), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result)
            : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsIssuesUpdate)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Result<GoodsIssueSummary>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Result<GoodsIssueSummary>>> Update(
        Guid id,
        [FromBody] SaveGoodsIssueRequest request,
        CancellationToken cancellationToken)
    {
        if (CreatesMasterData(request) && !await CanManageMasterDataAsync()) return Forbid();
        if (ReferencesNewProduct(request) && !await CanCreateProductsAsync()) return Forbid();
        var result = await sender.Send(ToCommand(id, request), cancellationToken);
        return result.IsSuccess
            ? Ok(result)
            : result.Messages.Contains(ConcurrencyMessages.Conflict, StringComparer.Ordinal)
                ? Conflict(result)
                : BadRequest(result);
    }

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsIssuesDelete)]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<Result>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteGoodsIssueCommand(id), cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [Authorize(Policy = PermissionPolicyNames.Inventory.GoodsIssuesExport)]
    [HttpGet("items/export")]
    public async Task<IActionResult> ExportItems(
        [FromQuery] GetGoodsIssueItemsQuery query,
        CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("خروج کالا");
        worksheet.RightToLeft = true;
        string[] headers = ["ردیف", "کد خروج", "تاریخ خروج", "خارج‌کننده", "توضیحات کلی",
            "کد کالا", "عنوان کالا", "تعداد", "شماره اموال", "مقصد", "مکان ارسال", "علت خروج",
            "توضیحات ردیف", "واحد تحویل‌دهنده", "برگشت داده می‌شود", "تاریخ عودت"];
        for (var column = 1; column <= headers.Length; column++)
            worksheet.Cell(1, column).Value = headers[column - 1];

        var row = 2;
        var page = 1;
        while (true)
        {
            var result = await sender.Send(query with { PageNumber = page, PageSize = ExportBatchSize }, cancellationToken);
            if (!result.IsSuccess || result.Data is null) return BadRequest(result);
            foreach (var item in result.Data.Items)
            {
                worksheet.Cell(row, 1).Value = row - 1;
                worksheet.Cell(row, 2).Value = item.IssueNumber;
                worksheet.Cell(row, 3).Value = FormatPersianDate(item.IssuedAtUtc);
                worksheet.Cell(row, 4).Value = item.IssuerFullName;
                worksheet.Cell(row, 5).Value = item.IssueDescription ?? string.Empty;
                worksheet.Cell(row, 6).Value = item.ProductCode;
                worksheet.Cell(row, 7).Value = item.ProductTitle ?? string.Empty;
                worksheet.Cell(row, 8).Value = item.Quantity;
                worksheet.Cell(row, 9).Value = item.AssetNumber ?? string.Empty;
                worksheet.Cell(row, 10).Value = item.DestinationTitle;
                worksheet.Cell(row, 11).Value = item.ShippingLocationTitle ?? string.Empty;
                worksheet.Cell(row, 12).Value = string.IsNullOrWhiteSpace(item.LegacyExitReason)
                    ? FormatExitReason(item.ExitReason)
                    : item.LegacyExitReason;
                worksheet.Cell(row, 13).Value = item.Description ?? string.Empty;
                worksheet.Cell(row, 14).Value = item.DeliveringUnit;
                worksheet.Cell(row, 15).Value = item.IsReturnable ? "بله" : "خیر";
                worksheet.Cell(row, 16).Value = item.ReturnAtUtc.HasValue ? FormatPersianDate(item.ReturnAtUtc.Value) : string.Empty;
                row++;
            }
            if (result.Data.Items.Count < ExportBatchSize || row - 2 >= result.Data.FilteredCount) break;
            page++;
        }
        var header = worksheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
        worksheet.Columns().AdjustToContents(8, 35);
        worksheet.SheetView.FreezeRows(1);
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"goods-issues-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    private static SaveGoodsIssueCommand ToCommand(Guid? id, SaveGoodsIssueRequest request) => new(
        id, request.IssueNumber, request.IssuedAtUtc, request.Description,
        request.IssuerId, request.IssuerFullName,
        request.Items.Select(item => new SaveGoodsIssueItem(
            item.ProductId, item.ProductCode, item.Quantity, item.AssetNumber, item.DestinationId,
            item.DestinationTitle, item.ExitReason, item.Description, item.DeliveringUnit,
            item.ReturnAtUtc, item.ProductTitle, item.UnitOfMeasureId,
            item.ShippingLocationId, item.IsReturnable)).ToArray(), request.ConcurrencyStamp);

    private static bool CreatesMasterData(SaveGoodsIssueRequest request) =>
        (!request.IssuerId.HasValue && !string.IsNullOrWhiteSpace(request.IssuerFullName)) ||
        request.Items.Any(item => !item.DestinationId.HasValue && !string.IsNullOrWhiteSpace(item.DestinationTitle));

    private static bool ReferencesNewProduct(SaveGoodsIssueRequest request) =>
        request.Items.Any(item => !item.ProductId.HasValue);

    private async Task<bool> CanManageMasterDataAsync() =>
        (await authorizationService.AuthorizeAsync(User, PermissionPolicyNames.MasterData.Manage)).Succeeded;

    private async Task<bool> CanCreateProductsAsync() =>
        (await authorizationService.AuthorizeAsync(User, PermissionPolicyNames.MasterData.ProductsCreate)).Succeeded;

    private static string FormatPersianDate(DateTimeOffset value)
    {
        var local = value.ToLocalTime();
        var calendar = new PersianCalendar();
        return $"{calendar.GetYear(local.DateTime):0000}/{calendar.GetMonth(local.DateTime):00}/{calendar.GetDayOfMonth(local.DateTime):00}";
    }

    private static string FormatExitReason(GoodsIssueExitReason reason) => reason switch
    {
        GoodsIssueExitReason.Maintenance => "تعمیرات",
        GoodsIssueExitReason.Sample => "نمونه",
        GoodsIssueExitReason.Purchase => "خرید",
        GoodsIssueExitReason.Sale => "فروش",
        GoodsIssueExitReason.Services => "خدمات",
        GoodsIssueExitReason.Consignment => "امانی",
        GoodsIssueExitReason.Repair => "تعمیر",
        _ => "سایر"
    };
}
