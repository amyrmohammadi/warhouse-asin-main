using System.Globalization;
using ClosedXML.Excel;
using Warehouse.Application.Common.Interfaces;

namespace Warehouse.Infrastructure.Documents;

public sealed class InvoicePdfService(IExcelToPdfService converter) : IInvoicePdfService
{
    public Task<byte[]> GenerateAsync(IReadOnlyList<InvoicePrintEntry> invoices, CancellationToken cancellationToken = default)
    {
        return converter.ConvertAsync(CreateWorkbook(invoices, cancellationToken), cancellationToken);
    }

    internal static byte[] CreateWorkbook(IReadOnlyList<InvoicePrintEntry> invoices, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (invoices.Count is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(invoices));
        using var template = typeof(InvoicePdfService).Assembly.GetManifestResourceStream(
            "Warehouse.Infrastructure.Documents.Templates.InvoiceFinanceDelivery.xlsx")
            ?? throw new InvalidOperationException("The invoice print template is missing.");
        using var workbook = new XLWorkbook(template);
        var source = workbook.Worksheet(1);
        var pages = (invoices.Count + 24) / 25;
        // Clone the untouched form so every page retains the heading and signatures.
        var sheets = new List<IXLWorksheet> { source };
        for (var page = 1; page < pages; page++) sheets.Add(source.CopyTo("صفحه " + (page + 1)));
        for (var page = 0; page < pages; page++)
        {
            token.ThrowIfCancellationRequested();
            var sheet = sheets[page];
            sheet.RightToLeft = true;
            sheet.PageSetup.PrintAreas.Clear();
            sheet.PageSetup.PrintAreas.Add("B1:E33");
            sheet.PageSetup.FitToPages(1, 1);
            var body = sheet.Range("B7:E31");
            body.Style.Font.FontSize = 11;
            body.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            body.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            body.Style.Alignment.WrapText = true;
            sheet.Range("C7:C31").Style.Alignment.ReadingOrder = XLAlignmentReadingOrderValues.LeftToRight;
            sheet.Range("D7:D31").Style.Alignment.ReadingOrder = XLAlignmentReadingOrderValues.RightToLeft;
            sheet.Range("E7:E31").Style.Alignment.ReadingOrder = XLAlignmentReadingOrderValues.LeftToRight;
            for (var slot = 0; slot < 25; slot++)
            {
                var row = slot + 7;
                var index = page * 25 + slot;
                sheet.Range(row, 2, row, 5).Clear(XLClearOptions.Contents);
                if (index >= invoices.Count) continue;
                var invoice = invoices[index];
                sheet.Cell(row, 2).Value = PersianDigits((index + 1).ToString(CultureInfo.InvariantCulture));
                sheet.Cell(row, 3).Value = invoice.FinanceDeliveryDate is { } date ? PersianDate(date) : string.Empty;
                sheet.Cell(row, 4).Value = invoice.Counterparty;
                sheet.Cell(row, 5).Value = invoice.InvoiceNumber;
                var lines = Math.Max((invoice.Counterparty.Length + 27) / 28, (invoice.InvoiceNumber.Length + 10) / 11);
                sheet.Row(row).Height = Math.Max(20, lines * 14 + 4);
            }
        }
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static string PersianDate(DateOnly date)
    {
        var value = date.ToDateTime(TimeOnly.MinValue);
        var calendar = new PersianCalendar();
        return PersianDigits($"{calendar.GetYear(value):0000}/{calendar.GetMonth(value):00}/{calendar.GetDayOfMonth(value):00}");
    }
    private static string PersianDigits(string value) => string.Concat(value.Select(character =>
        character is >= '0' and <= '9' ? (char)('۰' + character - '0') : character));
}
