using Warehouse.Domain.Common;
using Warehouse.Domain.Enums;
using InvoiceStatusValue = Warehouse.Domain.Enums.InvoiceStatus;

namespace Warehouse.Domain.Entities;

public sealed class GoodsReceiptItem : Entity<Guid>
{
    private GoodsReceiptItem()
    {
    }

    internal GoodsReceiptItem(
        Guid goodsReceiptId,
        Guid productId,
        Guid warehouseId,
        InvoiceStatus? invoiceStatus,
        Invoice? invoice,
        string? receiptNumber,
        decimal quantity,
        string? requestNumber,
        string? description,
        int sortOrder)
    {
        Id = Guid.NewGuid();
        GoodsReceiptId = goodsReceiptId;
        ProductId = productId;
        WarehouseId = warehouseId;
        SetReceiptInvoice(invoiceStatus, invoice?.Id, invoice, receiptNumber);
        Quantity = quantity;
        RequestNumber = Normalize(requestNumber);
        Description = Normalize(description);
        SortOrder = sortOrder;
    }

    public Guid GoodsReceiptId { get; private set; }

    public GoodsReceipt GoodsReceipt { get; private set; } = null!;

    public Guid ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    public Guid WarehouseId { get; private set; }

    public StorageWarehouse Warehouse { get; private set; } = null!;

    public InvoiceStatus? InvoiceStatus { get; private set; }

    public Guid? InvoiceId { get; private set; }

    public Invoice? Invoice { get; private set; }

    public Guid? SupplierId { get; private set; }

    public Supplier? Supplier { get; private set; }

    public void SetSupplier(Supplier? supplier)
    {
        Supplier = InvoiceStatus == InvoiceStatusValue.HasInvoice ? null : supplier;
        SupplierId = Supplier?.Id;
    }

    public Guid? GoodsIssueItemId { get; private set; }

    public GoodsIssueItem? GoodsIssueItem { get; private set; }

    public string? ReceiptNumber { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal? Price { get; private set; }

    public void UpdatePrice(decimal? price)
    {
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price));
        Price = price;
    }

    public string? RequestNumber { get; private set; }

    public string? Description { get; private set; }

    public int SortOrder { get; private set; }

    public void Update(Guid warehouseId, InvoiceStatus? invoiceStatus, Guid? invoiceId, Invoice? invoice,
        string? receiptNumber, decimal quantity, string? requestNumber,
        string? description, int sortOrder)
    {
        WarehouseId = warehouseId;
        SetReceiptInvoice(invoiceStatus, invoiceId, invoice, receiptNumber);
        Quantity = quantity;
        RequestNumber = Normalize(requestNumber);
        Description = Normalize(description);
        SortOrder = sortOrder;
    }

    public void UpdateReceiptNumber(string? receiptNumber) =>
        ReceiptNumber = Normalize(receiptNumber);

    public void SetGoodsIssueItem(GoodsIssueItem? goodsIssueItem)
    {
        GoodsIssueItem?.GoodsReceiptItems.Remove(this);
        GoodsIssueItem = goodsIssueItem;
        GoodsIssueItemId = goodsIssueItem?.Id;
        if (goodsIssueItem is not null && !goodsIssueItem.GoodsReceiptItems.Contains(this))
            goodsIssueItem.GoodsReceiptItems.Add(this);
    }

    private void SetReceiptInvoice(InvoiceStatus? status, Guid? invoiceId, Invoice? invoice,
        string? receiptNumber)
    {
        InvoiceStatus = status;
        Invoice = status == InvoiceStatusValue.HasInvoice ? invoice : null;
        InvoiceId = status == InvoiceStatusValue.HasInvoice ? invoiceId : null;
        if (status == InvoiceStatusValue.HasInvoice)
            SetSupplier(null);
        ReceiptNumber = Normalize(receiptNumber);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
