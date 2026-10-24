using Warehouse.Domain.Common;

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

    public Guid? SupplierId { get; private set; }

    public Supplier? Supplier { get; private set; }

    public ICollection<GoodsReceiptItem> GoodsReceiptItems { get; private set; } = [];

    public static string NormalizeNumber(string value) => value.Trim();
}
