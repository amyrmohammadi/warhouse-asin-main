using Warehouse.Domain.Common;
using Warehouse.Domain.Enums;

namespace Warehouse.Domain.Entities;

public sealed class GoodsReceipt : Entity<Guid>
{
    private GoodsReceipt()
    {
    }

    public GoodsReceipt(
        DateTimeOffset enteredAtUtc,
        string? description,
        Guid delivererId,
        Guid warehouseId,
        InvoiceStatus invoiceStatus,
        string? invoiceNumber,
        Guid? supplierId,
        string? receiptNumber,
        string pageNumber)
    {
        Id = Guid.NewGuid();
        EnteredAtUtc = enteredAtUtc.ToUniversalTime();
        Description = Normalize(description);
        DelivererId = delivererId;
        WarehouseId = warehouseId;
        InvoiceStatus = invoiceStatus;
        InvoiceNumber = invoiceStatus == InvoiceStatus.HasInvoice
            ? Normalize(invoiceNumber)
            : null;
        SupplierId = supplierId;
        ReceiptNumber = Normalize(receiptNumber);
        PageNumber = pageNumber.Trim();
    }

    public DateTimeOffset EnteredAtUtc { get; private set; }

    public string? Description { get; private set; }

    public Guid DelivererId { get; private set; }

    public Person Deliverer { get; private set; } = null!;

    public Guid WarehouseId { get; private set; }

    public StorageWarehouse Warehouse { get; private set; } = null!;

    public InvoiceStatus InvoiceStatus { get; private set; }

    public string? InvoiceNumber { get; private set; }

    public Guid? SupplierId { get; private set; }

    public Supplier? Supplier { get; private set; }

    public string? ReceiptNumber { get; private set; }

    public string PageNumber { get; private set; } = string.Empty;

    public ICollection<GoodsReceiptItem> Items { get; private set; } = [];

    public void AddItem(
        Guid productId,
        decimal quantity,
        string? requestNumber,
        string? description) =>
        Items.Add(new GoodsReceiptItem(
            Id,
            productId,
            quantity,
            requestNumber,
            description));

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
