using System.ComponentModel.DataAnnotations.Schema;
using Warehouse.Domain.Common;
namespace Warehouse.Domain.Entities;

public sealed class GoodsReceipt : ConcurrencyEntity<Guid>
{
    private GoodsReceipt()
    {
    }

    public GoodsReceipt(
        DateTimeOffset enteredAtUtc,
        string? description,
        Guid delivererId,
        string pageNumber)
    {
        Id = Guid.NewGuid();
        EnteredAtUtc = enteredAtUtc.ToUniversalTime();
        Description = Normalize(description);
        DelivererId = delivererId;
        PageNumber = pageNumber.Trim();
    }

    public DateTimeOffset EnteredAtUtc { get; private set; }

    public string? Description { get; private set; }

    public Guid DelivererId { get; private set; }

    public Person Deliverer { get; private set; } = null!;

    public string PageNumber { get; private set; } = string.Empty;

    public ICollection<GoodsReceiptItem> Items { get; private set; } = [];

    [NotMapped]
    public Guid? SupplierId => Items.FirstOrDefault()?.Invoice?.SupplierId;

    [NotMapped]
    public Supplier? Supplier => Items.FirstOrDefault()?.Invoice?.Supplier;

    public void AddItem(
        Guid productId,
        Guid warehouseId,
        Domain.Enums.InvoiceStatus? invoiceStatus,
        Invoice? invoice,
        string? receiptNumber,
        decimal quantity,
        string? requestNumber,
        string? description,
        int sortOrder) =>
        Items.Add(new GoodsReceiptItem(
            Id,
            productId,
            warehouseId,
            invoiceStatus,
            invoice,
            receiptNumber,
            quantity,
            requestNumber,
            description,
            sortOrder));

    public void UpdateInitialEntry(DateTimeOffset enteredAtUtc, string? description,
        Guid delivererId, string pageNumber)
    {
        EnteredAtUtc = enteredAtUtc.ToUniversalTime();
        Description = Normalize(description);
        DelivererId = delivererId;
        PageNumber = pageNumber.Trim();
    }
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
