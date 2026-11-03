using System.Globalization;
using ClosedXML.Excel;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Enums;

namespace Warehouse.Infrastructure.Documents;

public sealed class GoodsReceiptListPdfService(IExcelToPdfService converter) : IGoodsReceiptListPdfService
{
    public Task<byte[]> GenerateItemsAsync(IReadOnlyList<GoodsReceiptItemRowSummary> items,
        IReadOnlyList<string> columns, CancellationToken cancellationToken = default) =>
        GenerateAsync(items, columns, GoodsReceiptPrintColumns.Items, ItemValue, cancellationToken);

    public Task<byte[]> GenerateReceiptsAsync(IReadOnlyList<GoodsReceiptSummary> receipts,
        IReadOnlyList<string> columns, CancellationToken cancellationToken = default) =>
        GenerateAsync(receipts, columns, GoodsReceiptPrintColumns.Receipts, ReceiptValue, cancellationToken);

    private Task<byte[]> GenerateAsync<T>(IReadOnlyList<T> rows, IReadOnlyList<string> columns,
        IReadOnlyList<GoodsReceiptPrintColumn> definitions, Func<T, string, string?> value,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (columns.Count == 0 || columns.Distinct().Count() != columns.Count ||
            columns.Any(key => !definitions.Any(column => column.Key == key)))
            throw new ArgumentException("Invalid print columns.", nameof(columns));

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("ورودی کالا");
        sheet.RightToLeft = true;
        sheet.Style.Font.FontName = "Arial";
        sheet.Style.Font.FontSize = columns.Count > 10 ? 8 : 10;
        sheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Style.Alignment.WrapText = true;
        sheet.Cell(1, 1).Value = "گزارش ورودی کالا";
        sheet.Range(1, 1, 1, columns.Count).Merge().Style.Font.Bold = true;
        sheet.Row(1).Height = 28;
        for (var column = 0; column < columns.Count; column++)
        {
            var key = columns[column];
            sheet.Cell(2, column + 1).Value = definitions.Single(definition => definition.Key == key).Title;
            sheet.Column(column + 1).Width = key switch
            {
                "id" => 6,
                "quantity" or "itemCount" or "unitOfMeasureTitle" => 10,
                "itemDescription" or "description" or "itemTitles" or "productTitle" => 32,
                _ => 18
            };
        }
        sheet.Row(2).Height = 25;
        sheet.Range(2, 1, 2, columns.Count).Style.Font.Bold = true;
        sheet.Range(2, 1, 2, columns.Count).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
        for (var row = 0; row < rows.Count; row++)
        {
            token.ThrowIfCancellationRequested();
            var height = 24d;
            for (var column = 0; column < columns.Count; column++)
            {
                var text = columns[column] == "id" ? (row + 1).ToString(CultureInfo.InvariantCulture)
                    : value(rows[row], columns[column]) ?? string.Empty;
                sheet.Cell(row + 3, column + 1).Value = text;
                var charactersPerLine = Math.Max(6, (int)sheet.Column(column + 1).Width - 2);
                var lines = text.Split('\n').Sum(line => Math.Max(1, (line.Length + charactersPerLine - 1) / charactersPerLine));
                height = Math.Max(height, lines * 14 + 8);
            }
            sheet.Row(row + 3).Height = Math.Min(409, height);
        }
        var lastRow = Math.Max(2, rows.Count + 2);
        var table = sheet.Range(2, 1, lastRow, columns.Count);
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.PageOrientation = columns.Count > 6 ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(1, 2);
        sheet.PageSetup.PrintAreas.Add(1, 1, lastRow, columns.Count);
        sheet.PageSetup.Margins.Top = sheet.PageSetup.Margins.Bottom = .35;
        sheet.PageSetup.Margins.Left = sheet.PageSetup.Margins.Right = .25;
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return converter.ConvertAsync(output.ToArray(), token);
    }

    private static string? ItemValue(GoodsReceiptItemRowSummary item, string column) => column switch
    {
        "pageNumber" => item.PageNumber, "enteredAtUtc" => PersianDate(item.EnteredAtUtc),
        "productTitle" => item.ProductTitle, "productCode" => item.ProductCode,
        "quantity" => item.Quantity.ToString("#,##0.###", CultureInfo.InvariantCulture),
        "unitOfMeasureTitle" => item.UnitOfMeasureTitle, "itemDescription" => item.ItemDescription,
        "delivererFullName" => item.DelivererFullName,
        "invoiceStatus" => item.InvoiceStatus switch
        {
            InvoiceStatus.HasInvoice => "دارد", InvoiceStatus.DoesNotHave => "ندارد", _ => "وارد نشده"
        },
        "invoiceNumber" => item.InvoiceNumber, "counterparty" => item.Counterparty,
        "receiptNumber" => item.ReceiptNumber, "issueNumber" => item.IssueNumber,
        "warehouseTitle" => item.WarehouseTitle, _ => null
    };

    private static string? ReceiptValue(GoodsReceiptSummary receipt, string column) => column switch
    {
        "enteredAtUtc" => PersianDate(receipt.EnteredAtUtc), "pageNumber" => receipt.PageNumber,
        "delivererFullName" => receipt.DelivererFullName,
        "itemCount" => receipt.Items.Count.ToString(CultureInfo.InvariantCulture),
        "itemTitles" => string.Join("، ", receipt.Items.Select(item => item.ProductTitle)),
        "description" => receipt.Description, _ => null
    };

    private static string PersianDate(DateTimeOffset value)
    {
        var local = TimeZoneInfo.ConvertTime(value, TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran")).DateTime;
        var calendar = new PersianCalendar();
        return FormattableString.Invariant($"{calendar.GetYear(local):0000}/{calendar.GetMonth(local):00}/{calendar.GetDayOfMonth(local):00}");
    }
}
