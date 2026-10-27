using System.Globalization;
using ClosedXML.Excel;

namespace Warehouse.Application.Features.Inventory.GoodsIssues.Commands.ImportGoodsIssues;

internal static class GoodsIssueExcelReader
{
    internal sealed record Row(string Sheet, int Number, string IssueNumber, DateTimeOffset? Date,
        string ProductTitle, decimal? Quantity, string Unit, string Issuer, string Destination,
        string ExitReason, string DeliveringUnit, DateTimeOffset? ReturnAtUtc, string Description);

    public static List<Row> Read(byte[] content, CancellationToken token)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var workbook = new XLWorkbook(stream);
        var result = new List<Row>();
        foreach (var sheet in workbook.Worksheets)
        {
            token.ThrowIfCancellationRequested();
            var header = sheet.Row(1).CellsUsed(XLCellsUsedOptions.Contents)
                .ToDictionary(cell => HeaderKey(cell.GetString()), cell => cell.Address.ColumnNumber);
            int Column(params string[] names) => names.Select(name => header.GetValueOrDefault(HeaderKey(name)))
                .FirstOrDefault(number => number > 0);
            var issueColumn = Column("شماره خروج", "کد خروج");
            var dateColumn = Column("تاریخ", "تاریخ خروج");
            var titleColumn = Column("عنوان کالا");
            var quantityColumn = Column("تعداد", "مقدار");
            var unitColumn = Column("واحد کالا", "واحد");
            var issuerColumn = Column("خارج کننده", "خارج‌کننده");
            if (issueColumn == 0 || dateColumn == 0 || titleColumn == 0 || quantityColumn == 0 || issuerColumn == 0)
                throw new FormatException($"ستون‌های اصلی شیت «{sheet.Name}» کامل نیستند.");
            var destinationColumn = Column("مقصد");
            var reasonColumn = Column("علت خروج", "شرح");
            var deliveringColumn = Column("واحد تحویل دهنده", "واحد تحویل‌دهنده");
            var returnColumn = Column("تاریخ عودت");
            var descriptionColumn = Column("توضیحات");
            var returnerColumn = Column("عودت دهنده");
            var repairsColumn = Column("تعمیریها");
            var counterpartInvoiceColumn = Column("شماره فاکتور طرف مقابل");
            var addressColumn = Column("آدرس");
            var repairerColumn = Column("نام تعمیرکننده");
            var lastRow = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 1;
            for (var number = 2; number <= lastRow; number++)
            {
                token.ThrowIfCancellationRequested();
                string Text(int column) => column == 0 ? string.Empty : sheet.Cell(number, column).GetFormattedString().Trim();
                var title = Text(titleColumn);
                var issue = Text(issueColumn);
                var quantityText = Text(quantityColumn);
                if (title.Length == 0 && issue.Length == 0 && quantityText.Length == 0) continue;
                var normalizedQuantity = Digits(quantityText).Replace(",", "").Replace("٬", "").Replace("٫", ".");
                var quantity = decimal.TryParse(normalizedQuantity, NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var parsed) ? parsed : (decimal?)null;
                var details = new List<string>();
                if (Text(descriptionColumn).Length > 0) details.Add(Text(descriptionColumn));
                void AddDetail(string label, int column)
                {
                    var value = Text(column);
                    if (value.Length > 0) details.Add($"{label}: {value}");
                }
                AddDetail("عودت‌دهنده", returnerColumn);
                AddDetail("تعمیری‌ها", repairsColumn);
                AddDetail("شماره فاکتور طرف مقابل", counterpartInvoiceColumn);
                AddDetail("آدرس", addressColumn);
                AddDetail("نام تعمیرکننده", repairerColumn);
                result.Add(new Row(sheet.Name, number, NormalizeIssueNumber(sheet.Cell(number, issueColumn)),
                    ReadDate(sheet.Cell(number, dateColumn)), title, quantity, Text(unitColumn), Text(issuerColumn),
                    Text(destinationColumn), Text(reasonColumn), Text(deliveringColumn),
                    returnColumn == 0 ? null : ReadDate(sheet.Cell(number, returnColumn)),
                    string.Join(" | ", details)));
            }
        }
        return result;
    }

    private static string HeaderKey(string value) =>
        string.Concat(value.Trim().Replace('ي', 'ی').Replace('ك', 'ک')
            .Where(character => !char.IsWhiteSpace(character) && character != '\u200C'));

    private static string NormalizeIssueNumber(IXLCell cell)
    {
        var text = Digits(cell.GetFormattedString().Trim());
        if (cell.DataType == XLDataType.Number && cell.GetDouble() is var number &&
            number >= 0 && number < 1000 && number != Math.Truncate(number))
        {
            var parts = number.ToString("0.00000", CultureInfo.InvariantCulture).Split('.');
            return $"{parts[0]}/{parts[1]}";
        }
        if (text.Length == 7 && text.All(char.IsAsciiDigit) && text.StartsWith("99", StringComparison.Ordinal))
            return $"{text[..2]}/{text[2..]}";
        var dot = text.IndexOf('.');
        if (dot == 2 && text.Length is >= 4 and <= 8 && text[..dot].All(char.IsAsciiDigit) &&
            text[(dot + 1)..].All(char.IsAsciiDigit))
            return $"{text[..dot]}/{text[(dot + 1)..].PadRight(5, '0')}";
        return text;
    }

    private static DateTimeOffset? ReadDate(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        try
        {
            DateTime date;
            if (cell.DataType == XLDataType.DateTime) date = cell.GetDateTime();
            else if (cell.DataType == XLDataType.Number) date = DateTime.FromOADate(cell.GetDouble());
            else
            {
                var text = Digits(cell.GetFormattedString().Trim()).Replace('-', '/').Replace('.', '/');
                var parts = text.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3 || !parts.All(part => int.TryParse(part, out _))) return null;
                var values = parts.Select(int.Parse).ToArray();
                date = values[0] is >= 1200 and <= 1600
                    ? new PersianCalendar().ToDateTime(values[0], values[1], values[2], 0, 0, 0, 0)
                    : new DateTime(values[0], values[1], values[2]);
            }
            var local = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
            return zone.IsInvalidTime(local) ? null : new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
        }
        catch (ArgumentException) { return null; }
        catch (FormatException) { return null; }
        catch (OverflowException) { return null; }
    }

    private static string Digits(string value) => string.Concat(value.Select(character =>
        character is >= '۰' and <= '۹' ? (char)('0' + character - '۰') :
        character is >= '٠' and <= '٩' ? (char)('0' + character - '٠') : character));
}
