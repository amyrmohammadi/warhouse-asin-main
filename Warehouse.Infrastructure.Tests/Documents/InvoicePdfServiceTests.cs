using ClosedXML.Excel;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Infrastructure.Documents;
using Xunit;

namespace Warehouse.Infrastructure.Tests.Documents;

public sealed class InvoicePdfServiceTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(25, 1)]
    [InlineData(26, 2)]
    [InlineData(51, 3)]
    public async Task Generate_PreservesTemplateAndPaginatesWithoutLosingInvoices(int count, int pageCount)
    {
        var converter = new Converter();
        var invoices = Enumerable.Range(1, count).Select(index => new InvoicePrintEntry(
            "INV-" + index, "طرف حساب " + index, new DateOnly(2026, 10, 31))).ToArray();
        await new InvoicePdfService(converter).GenerateAsync(invoices);
        using var workbook = new XLWorkbook(new MemoryStream(converter.Workbook!));
        Assert.Equal(pageCount, workbook.Worksheets.Count);
        for (var index = 0; index < count; index++)
        {
            var sheet = workbook.Worksheet(index / 25 + 1);
            var row = index % 25 + 7;
            Assert.Equal("INV-" + (index + 1), sheet.Cell(row, 5).GetString());
            Assert.Equal("طرف حساب " + (index + 1), sheet.Cell(row, 4).GetString());
            Assert.Equal("۱۴۰۵/۰۸/۰۹", sheet.Cell(row, 3).GetString());
        }
        foreach (var sheet in workbook.Worksheets)
        {
            Assert.Equal("اسناد تحویلی به واحد مالی", sheet.Cell("B1").GetString());
            Assert.Equal("امضاء تحویل دهنده:", sheet.Cell("B33").GetString());
            Assert.Equal("امضاء تحویل گیرنده:", sheet.Cell("D33").GetString());
            Assert.True(sheet.RightToLeft);
            Assert.Equal(1, sheet.PageSetup.PagesWide);
            Assert.Equal(1, sheet.PageSetup.PagesTall);
            Assert.Equal("$B$1:$E$33", Assert.Single(sheet.PageSetup.PrintAreas).RangeAddress.ToString());
        }
        if (count % 25 != 0)
            Assert.True(workbook.Worksheet(pageCount).Cell(count % 25 + 7, 5).IsEmpty());
        await ExcelInputValidator.ValidateAsync(converter.Workbook!, new LibreOfficeOptions(), default);
    }

    [Fact]
    public async Task Generate_LeavesUnsetDatesBlank_AndTreatsInvoiceNumbersAsText()
    {
        var converter = new Converter();
        await new InvoicePdfService(converter).GenerateAsync([new("=1+2", "<طرف>&حساب", null)]);
        using var workbook = new XLWorkbook(new MemoryStream(converter.Workbook!));
        Assert.True(workbook.Worksheet(1).Cell("C7").IsEmpty());
        Assert.Equal("=1+2", workbook.Worksheet(1).Cell("E7").GetString());
        Assert.False(workbook.Worksheet(1).Cell("E7").HasFormula);
    }

    private sealed class Converter : IExcelToPdfService
    {
        public byte[]? Workbook { get; private set; }
        public Task<byte[]> ConvertAsync(byte[] excelBytes, CancellationToken cancellationToken = default)
        {
            Workbook = excelBytes;
            return Task.FromResult(new byte[] { 1 });
        }
    }
}
