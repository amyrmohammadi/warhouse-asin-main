using Warehouse.Domain.Common;
using Warehouse.Domain.Enums;

namespace Warehouse.Domain.Entities;

public sealed class Invoice : Entity<Guid>
{
    private Invoice()
    {
    }

    public Invoice(string invoiceNumber, Guid? supplierId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);
        Id = Guid.NewGuid();
        InvoiceNumber = NormalizeNumber(invoiceNumber);
        SupplierId = supplierId;
    }

    public string InvoiceNumber { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal? Price { get; private set; }

    public void UpdatePrice(decimal? price)
    {
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price));
        Price = price;
    }

    public DateOnly? InvoiceDate { get; private set; }

    public InvoiceProcessingStatus ProcessingStatus { get; private set; } = InvoiceProcessingStatus.UnderReview;

    public Guid? SupplierId { get; private set; }

    public Supplier? Supplier { get; private set; }

    public ICollection<GoodsReceiptItem> GoodsReceiptItems { get; private set; } = [];

    public void UpdateDetails(string? description, DateOnly? invoiceDate, InvoiceProcessingStatus processingStatus)
    {
        if (!Enum.IsDefined(processingStatus))
            throw new ArgumentOutOfRangeException(nameof(processingStatus));

        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        InvoiceDate = invoiceDate;
        ProcessingStatus = processingStatus;
    }

    public void UpdateHeader(string invoiceNumber, Guid? supplierId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);
        InvoiceNumber = NormalizeNumber(invoiceNumber);
        SupplierId = supplierId;
    }

    public static string NormalizeNumber(string value) => value.Trim();
}
