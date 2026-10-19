
using System.Globalization;
using ClosedXML.Excel;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.ImportGoodsReceipts;

internal static class GoodsReceiptExcelReader
{
    internal sealed record Row(
        int Number,
        DateTimeOffset? Date,
        string? RequestNumber,
        string Title,
        string Code,
        decimal? Quantity,
        string Unit,
        string Description,
        string Deliverer,
        string Invoice,
        string InvoiceNumber,
        string Supplier,
        string ReceiptNumber,
        string Warehouse,
        string PageNumber);

    public static List<Row> Read(
        byte[] content,
        CancellationToken token)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var workbook = new XLWorkbook(stream);

        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new FormatException("فایل شیت ندارد.");

        var rows = new List<Row>();

        var lastRow = sheet
            .LastRowUsed(XLCellsUsedOptions.Contents)?
            .RowNumber() ?? 0;

        // ردیف اول هدر است
        for (var number = 2; number <= lastRow; number++)
        {
            token.ThrowIfCancellationRequested();

            string Text(int column) =>
                sheet.Cell(number, column)
                    .GetFormattedString()
                    .Trim();

            // ستون C: شماره درخواست
            var requestNumber = Text(3);

            // ستون D: عنوان کالا
            var title = Text(4);

            if (string.IsNullOrWhiteSpace(title))
                break;

            // ستون F: مقدار
            var quantityText = Digits(Text(6))
                .Replace(",", "")
                .Replace("٬", "")
                .Replace("٫", ".");

            var quantity = decimal.TryParse(
                quantityText,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed)
                    ? parsed
                    : (decimal?)null;

            rows.Add(new Row(
                Number: number,
                Date: ReadDate(sheet.Cell(number, 2)),
                RequestNumber: string.IsNullOrWhiteSpace(requestNumber)
                    ? null
                    : requestNumber,
                Title: title,
                Code: Text(5),
                Quantity: quantity,
                Unit: Text(7),
                Description: Text(8),
                Deliverer: Text(9),
                Invoice: Text(10),
                InvoiceNumber: Text(11),
                Supplier: Text(12),
                ReceiptNumber: Text(13),
                Warehouse: Text(14),
                PageNumber: Text(15)
            ));
        }

        return rows;
    }

    private static DateTimeOffset? ReadDate(IXLCell cell)
    {
        try
        {
            DateTime date;

            if (cell.DataType == XLDataType.DateTime)
            {
                date = cell.GetDateTime();
            }
            else if (cell.DataType == XLDataType.Number)
            {
                date = DateTime.FromOADate(cell.GetDouble());
            }
            else
            {
                var value = Digits(
                    cell.GetFormattedString().Trim());

                var parts = value
                    .Replace('-', '/')
                    .Split(
                        ['/', ' ', ':', 'T'],
                        StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 3 ||
                    !parts.All(p => int.TryParse(p, out _)))
                    return null;

                var values = parts.Select(int.Parse).ToArray();

                if (values.Length > 6)
                    return null;

                var hour = values.Length > 3 ? values[3] : 0;
                var minute = values.Length > 4 ? values[4] : 0;
                var second = values.Length > 5 ? values[5] : 0;

                date = values[0] is >= 1200 and <= 1600
                    ? new PersianCalendar().ToDateTime(
                        values[0],
                        values[1],
                        values[2],
                        hour,
                        minute,
                        second,
                        0)
                    : new DateTime(
                        values[0],
                        values[1],
                        values[2],
                        hour,
                        minute,
                        second);
            }

            var local = DateTime.SpecifyKind(
                date,
                DateTimeKind.Unspecified);

            var zone = TimeZoneInfo
                .FindSystemTimeZoneById("Iran Standard Time");

            if (zone.IsInvalidTime(local))
                return null;

            return new DateTimeOffset(
                local,
                zone.GetUtcOffset(local))
                .ToUniversalTime();
        }
        catch (ArgumentException) { return null; }
        catch (FormatException) { return null; }
        catch (OverflowException) { return null; }
    }

    private static string Digits(string value) =>
        string.Concat(value.Select(c =>
            c is >= '۰' and <= '۹'
                ? (char)('0' + c - '۰')
                : c is >= '٠' and <= '٩'
                    ? (char)('0' + c - '٠')
                    : c));
}
