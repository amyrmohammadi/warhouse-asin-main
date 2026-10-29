using Warehouse.Domain.Entities;

namespace Warehouse.Application.Tests.Features.Inventory.Invoices;

public sealed class InvoiceReceiptDateTests
{
    [Theory]
    [InlineData("2026-10-20T20:29:00Z", 20)]
    [InlineData("2026-10-20T20:30:00Z", 21)]
    [InlineData("2026-10-21T15:00:00+03:30", 21)]
    public void ReceiptDay_UsesTehranCalendarDay(string timestamp, int expectedDay)
    {
        Assert.Equal(new DateOnly(2026, 10, expectedDay),
            Invoice.GetReceiptDate(DateTimeOffset.Parse(timestamp)));
    }

    [Fact]
    public void EditingInvoiceDate_DoesNotChangeReceiptDay()
    {
        var receiptDate = new DateOnly(2026, 10, 20);
        var invoice = new Invoice("INV-1", null, receiptDate);
        invoice.UpdateDetails(null, receiptDate.AddDays(5), invoice.ProcessingStatus);
        Assert.Equal(receiptDate, invoice.ReceiptDate);
    }
}
