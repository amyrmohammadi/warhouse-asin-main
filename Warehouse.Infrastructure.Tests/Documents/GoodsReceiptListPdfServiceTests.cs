using ClosedXML.Excel;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Infrastructure.Documents;
using Xunit;

namespace Warehouse.Infrastructure.Tests.Documents;

public sealed class GoodsReceiptListPdfServiceTests
{
    [Fact]
    public async Task Items_OnlyPrintsSelectedColumnsInTheirOrder_AndRepeatsHeadersAcrossPages()
    {
        var converter = new Converter();
        var rows = Enumerable.Range(1, 501).Select(index => new GoodsReceiptItemRowSummary
        {
            ProductTitle = "کالا " + index, ReceiptNumber = "SECRET", Quantity = 1234.5m,
            EnteredAtUtc = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero)
        }).ToArray();
        await new GoodsReceiptListPdfService(converter).GenerateItemsAsync(rows,
            ["productTitle", "quantity", "id", "enteredAtUtc"]);
        using var workbook = new XLWorkbook(new MemoryStream(converter.Workbook!));
        var sheet = workbook.Worksheet(1);
        Assert.Equal(["عنوان کالا", "تعداد", "ردیف", "تاریخ"], sheet.Row(2).Cells(1, 4).Select(cell => cell.GetString()));
        Assert.Equal(4, sheet.LastColumnUsed()!.ColumnNumber());
        Assert.Equal("کالا 501", sheet.Cell(503, 1).GetString());
        Assert.Equal("1,234.5", sheet.Cell(3, 2).GetString());
        Assert.Equal("501", sheet.Cell(503, 3).GetString());
        Assert.Equal("1405/08/12", sheet.Cell(3, 4).GetString());
        Assert.DoesNotContain(sheet.CellsUsed(), cell => cell.GetString().Contains("SECRET"));
        Assert.True(sheet.RightToLeft);
        Assert.Equal(1, sheet.PageSetup.PagesWide);
        Assert.Equal(0, sheet.PageSetup.PagesTall);
        Assert.Equal(1, sheet.PageSetup.FirstRowToRepeatAtTop);
        Assert.Equal(2, sheet.PageSetup.LastRowToRepeatAtTop);
        await ExcelInputValidator.ValidateAsync(converter.Workbook!, new LibreOfficeOptions(), default);
    }

    [Fact]
    public async Task Receipts_PrintsTheSelectedDocumentColumns_AndTreatsContentAsText()
    {
        var converter = new Converter();
        await new GoodsReceiptListPdfService(converter).GenerateReceiptsAsync([
            new GoodsReceiptSummary { PageNumber = "=1+2", Description = "شرح", Items = [new() { ProductTitle = "کالا" }] }
        ], ["pageNumber", "itemCount", "itemTitles", "description"]);
        using var workbook = new XLWorkbook(new MemoryStream(converter.Workbook!));
        var sheet = workbook.Worksheet(1);
        Assert.Equal("=1+2", sheet.Cell(3, 1).GetString());
        Assert.False(sheet.Cell(3, 1).HasFormula);
        Assert.Equal("1", sheet.Cell(3, 2).GetString());
        Assert.Equal("کالا", sheet.Cell(3, 3).GetString());
        Assert.Equal("شرح", sheet.Cell(3, 4).GetString());
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
