using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Enums;
using Warehouse.Infrastructure.Documents;
using Xunit;

namespace Warehouse.Infrastructure.Tests.Documents;

public sealed class GoodsIssuePdfServiceTests
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Fact]
    public async Task Generate_FillsTheProvidedTemplate_AndPassesItToPdfConverter()
    {
        var converter = new RecordingConverter();
        var issue = Issue([Item(1)]);
        var pdf = await new GoodsIssuePdfService(converter).GenerateAsync(issue);

        Assert.Equal(converter.Pdf, pdf);
        using var zip = new ZipArchive(new MemoryStream(converter.Workbook!));
        var sheet = Read(zip, "xl/worksheets/sheet1.xml");
        Assert.Equal("شماره: OUT-433", Text(sheet, "A6"));
        Assert.Equal("تاریخ: ۱۴۰۵/۰۸/۰۷", Text(sheet, "E6"));
        Assert.Equal("۱", Text(sheet, "A9"));
        Assert.Contains("کالای 1", Text(sheet, "B9"));
        Assert.DoesNotContain("P-1", Text(sheet, "B9"));
        Assert.Equal("۲.۵", Text(sheet, "D9"));
        Assert.Equal("عدد", Text(sheet, "E9"));
        Assert.Equal("دارد", Text(sheet, "F9"));
        Assert.Equal("۱۴۰۵/۰۸/۰۶", Text(sheet, "G9"));
        Assert.Contains("علی رضایی", Text(sheet, "A20"));
        Assert.Contains("علی رضایی", Text(sheet, "A21"));
        Assert.Equal("", Text(sheet, "A10"));
        Assert.Equal("1", sheet.Root!.Element(Main + "sheetViews")!.Element(Main + "sheetView")!.Attribute("rightToLeft")!.Value);
        Assert.Equal("11", sheet.Root.Element(Main + "pageSetup")!.Attribute("paperSize")!.Value);
        Assert.Contains(sheet.Descendants(Main + "mergeCell"), cell => (string?)cell.Attribute("ref") == "B9:C9");
        Assert.Contains("$A$1:$J$21", Read(zip, "xl/workbook.xml").ToString());
        await ExcelInputValidator.ValidateAsync(converter.Workbook!, new LibreOfficeOptions(), default);
    }

    [Fact]
    public async Task Generate_ClonesCompleteFormsForMoreThanElevenRows_AndRetainsLogoAndPrinterSettings()
    {
        var issue = Issue(Enumerable.Range(1, 12).Select(Item).ToArray());
        var converter = new RecordingConverter();
        await new GoodsIssuePdfService(converter).GenerateAsync(issue);
        using var zip = new ZipArchive(new MemoryStream(converter.Workbook!));
        var workbook = Read(zip, "xl/workbook.xml");
        Assert.Equal(2, workbook.Root!.Element(Main + "sheets")!.Elements().Count());
        Assert.Equal("۱۲", Text(Read(zip, "xl/worksheets/sheet2.xml"), "A9"));
        Assert.Equal("", Text(Read(zip, "xl/worksheets/sheet2.xml"), "A10"));
        var areas = workbook.Descendants(Main + "definedName").Where(name => (string?)name.Attribute("name") == "_xlnm.Print_Area").ToArray();
        Assert.Equal(2, areas.Length);
        Assert.Equal("1", areas[1].Attribute("localSheetId")!.Value);
        Assert.Contains("drawing2.xml", Read(zip, "xl/worksheets/_rels/sheet2.xml.rels").ToString());
        using var original = Template();
        foreach (var part in new[] { "xl/media/image1.png", "xl/drawings/drawing1.xml", "xl/printerSettings/printerSettings1.bin" })
            Assert.Equal(Bytes(original, part), Bytes(zip, part));
        Assert.Equal(Bytes(original, "xl/drawings/drawing1.xml"), Bytes(zip, "xl/drawings/drawing2.xml"));
        using var package = SpreadsheetDocument.Open(new MemoryStream(converter.Workbook!), false);
        Assert.Equal(2, package.WorkbookPart!.WorksheetParts.Count());
        Assert.All(package.WorkbookPart.WorksheetParts, part => Assert.NotNull(part.DrawingsPart));
        foreach (var part in package.WorkbookPart.WorksheetParts)
        {
            var options = part.Worksheet.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.PrintOptions>()!;
            Assert.True(options.HorizontalCentered!.Value);
            var margins = part.Worksheet.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.PageMargins>()!;
            Assert.Equal(0.2, margins.Left!.Value);
            Assert.Equal(margins.Left.Value, margins.Right!.Value);
            var setup = part.Worksheet.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.PageSetup>()!;
            Assert.Equal(11U, setup.PaperSize!.Value);
            Assert.Equal(1U, setup.FitToWidth!.Value);
            Assert.Equal(1U, setup.FitToHeight!.Value);
            Assert.Null(setup.Id);
            Assert.Null(setup.Scale);
        }
        await ExcelInputValidator.ValidateAsync(converter.Workbook!, new LibreOfficeOptions(), default);
    }

    [Fact]
    public async Task Generate_LongDescriptionsContinueWithoutRepeatingQuantities_AndAreSafeText()
    {
        var originalText = string.Join(' ', Enumerable.Range(1, 150).Select(index => $"متن{index}"));
        var issue = Issue([new GoodsIssueItemSummary
        {
            ProductTitle = "=HYPERLINK(\"https://example.invalid\")", ProductCode = "<کد>&",
            Quantity = 8, Description = originalText, ExitReason = GoodsIssueExitReason.Other,
            IsReturnable = false, ReturnAtUtc = DateTimeOffset.UtcNow
        }]);
        var converter = new RecordingConverter();
        await new GoodsIssuePdfService(converter).GenerateAsync(issue);
        using var zip = new ZipArchive(new MemoryStream(converter.Workbook!));
        var sheets = zip.Entries.Where(entry => entry.FullName.StartsWith("xl/worksheets/sheet") && entry.FullName.EndsWith(".xml"))
            .Select(entry => Read(zip, entry.FullName)).ToArray();
        var descriptions = string.Join(' ', sheets.SelectMany(sheet => sheet.Descendants(Main + "c"))
            .Where(cell => ((string?)cell.Attribute("r"))?.StartsWith('H') == true)
            .Select(cell => string.Concat(cell.Descendants(Main + "t").Select(text => text.Value))));
        Assert.Contains("متن1", descriptions);
        Assert.Contains("متن150", descriptions);
        foreach (var word in originalText.Split(' ')) Assert.Contains(word, descriptions);
        Assert.Single(sheets.SelectMany(sheet => sheet.Descendants(Main + "c")), cell =>
            ((string?)cell.Attribute("r"))?.StartsWith('D') == true && cell.Descendants(Main + "t").Any(t => t.Value == "۸"));
        Assert.Equal("ندارد", Text(sheets[0], "F9"));
        Assert.Equal("", Text(sheets[0], "G9"));
        Assert.Empty(sheets.SelectMany(sheet => sheet.Descendants(Main + "f")));
        Assert.Contains("https://example.invalid", Text(sheets[0], "B9").Replace("\n", ""));
    }

    [Fact]
    public async Task Generate_CancelledRequestDoesNotCallConverter()
    {
        var converter = new RecordingConverter();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GoodsIssuePdfService(converter).GenerateAsync(Issue([]), cancellation.Token));
        Assert.Null(converter.Workbook);
    }

    [Fact]
    public async Task Generate_PersianLabelsAndTrailingPunctuationHaveExplicitRtlDirection()
    {
        var converter = new RecordingConverter();
        await new GoodsIssuePdfService(converter).GenerateAsync(Issue([
            new GoodsIssueItemSummary { ProductTitle = "عنوان کالا.", Description = "توضیح فارسی:", Quantity = 1 }
        ]));
        using var zip = new ZipArchive(new MemoryStream(converter.Workbook!));
        var sheet = Read(zip, "xl/worksheets/sheet1.xml");
        var formats = Read(zip, "xl/styles.xml").Root!.Element(Main + "cellXfs")!.Elements().ToArray();
        foreach (var address in new[] { "A6", "A7", "B9", "H9", "A20", "A21", "D21", "F21", "I21" })
        {
            var cell = sheet.Descendants(Main + "c").Single(cell => (string?)cell.Attribute("r") == address);
            var alignment = formats[(int)cell.Attribute("s")!].Element(Main + "alignment")!;
            Assert.Equal("2", (string?)alignment.Attribute("readingOrder"));
        }
        Assert.Equal("عنوان کالا.", Text(sheet, "B9"));
        Assert.Equal("توضیح فارسی:", Text(sheet, "H9"));
        var englishCell = sheet.Descendants(Main + "c").Single(cell => (string?)cell.Attribute("r") == "I4");
        Assert.NotEqual("2", (string?)formats[(int)englishCell.Attribute("s")!].Element(Main + "alignment")?.Attribute("readingOrder"));
    }

    [Fact]
    public async Task Generate_OnlyFillsFieldsPresentInTheForm_AndUsesConsistentDataFonts()
    {
        var issue = new GoodsIssueSummary
        {
            IssueNumber = "OUT-1", IssuerFullName = "نام خارج‌کننده", IssuedAtUtc = DateTimeOffset.UtcNow,
            Description = "نباید در توضیحات ردیف چاپ شود",
            Items = Enumerable.Range(1, 3).Select(index => new GoodsIssueItemSummary
            {
                ProductTitle = $"کالای {index}", ProductCode = "EXTRA-CODE", Quantity = index,
                ProductUnitOfMeasureTitle = "عدد", Description = index == 1 ? "توضیحات همین قلم" : null,
                AssetNumber = "EXTRA-ASSET", DestinationTitle = "مقصد اضافی", ShippingLocationTitle = "محل اضافی",
                DeliveringUnit = "واحد اضافی", LegacyExitReason = "علت اضافی"
            }).ToArray()
        };
        var converter = new RecordingConverter();
        await new GoodsIssuePdfService(converter).GenerateAsync(issue);
        using var zip = new ZipArchive(new MemoryStream(converter.Workbook!));
        var sheet = Read(zip, "xl/worksheets/sheet1.xml");
        Assert.Equal("کالای 1", Text(sheet, "B9"));
        Assert.Equal("توضیحات همین قلم", Text(sheet, "H9"));
        Assert.Equal("", Text(sheet, "H10"));
        Assert.Equal("", Text(sheet, "H11"));
        Assert.Equal("", Text(sheet, "A12"));
        var data = string.Join(' ', sheet.Descendants(Main + "t").Select(text => text.Value));
        foreach (var extra in new[] { issue.Description, "EXTRA-CODE", "EXTRA-ASSET", "مقصد اضافی", "محل اضافی", "واحد اضافی", "علت اضافی" })
            Assert.DoesNotContain(extra!, data);
        var styles = Read(zip, "xl/styles.xml");
        var formats = styles.Root!.Element(Main + "cellXfs")!.Elements().ToArray();
        var fonts = styles.Root.Element(Main + "fonts")!.Elements().ToArray();
        foreach (var row in new[] { 9, 10, 11 })
        foreach (var column in new[] { "A", "B", "D", "E", "F", "G", "H" })
        {
            var cell = sheet.Descendants(Main + "c").Single(cell => (string?)cell.Attribute("r") == column + row);
            var format = formats[(int)cell.Attribute("s")!];
            Assert.Equal(column == "G" ? "20" : "24", (string?)fonts[(int)format.Attribute("fontId")!].Element(Main + "sz")!.Attribute("val"));
        }
    }

    private static GoodsIssueSummary Issue(IReadOnlyCollection<GoodsIssueItemSummary> items) => new()
    {
        Id = Guid.NewGuid(), IssueNumber = "OUT-433", IssuerFullName = "علی رضایی",
        IssuedAtUtc = new DateTimeOffset(2026, 10, 28, 21, 0, 0, TimeSpan.Zero), Items = items
    };

    private static GoodsIssueItemSummary Item(int index) => new()
    {
        ProductTitle = $"کالای {index}", ProductCode = $"P-{index}", Quantity = 2.5m,
        ProductUnitOfMeasureTitle = "عدد", IsReturnable = true,
        ReturnAtUtc = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero), ExitReason = GoodsIssueExitReason.Repair
    };

    private static string Text(XDocument sheet, string address) => string.Concat(sheet.Descendants(Main + "c")
        .Single(cell => (string?)cell.Attribute("r") == address).Descendants(Main + "t").Select(element => element.Value));
    private static XDocument Read(ZipArchive zip, string path)
    {
        using var content = zip.GetEntry(path)!.Open();
        return XDocument.Load(content);
    }
    private static byte[] Bytes(ZipArchive zip, string path)
    {
        using var content = zip.GetEntry(path)!.Open();
        using var copy = new MemoryStream();
        content.CopyTo(copy);
        return copy.ToArray();
    }
    private static ZipArchive Template() => new(typeof(GoodsIssuePdfService).Assembly.GetManifestResourceStream(
        "Warehouse.Infrastructure.Documents.Templates.GoodsIssuePermit.xlsx")!, ZipArchiveMode.Read);

    private sealed class RecordingConverter : IExcelToPdfService
    {
        public byte[]? Workbook;
        public byte[] Pdf = "%PDF-1.7\n%%EOF"u8.ToArray();
        public Task<byte[]> ConvertAsync(byte[] excelBytes, CancellationToken cancellationToken = default)
        {
            Workbook = excelBytes;
            return Task.FromResult(Pdf);
        }
    }
}
