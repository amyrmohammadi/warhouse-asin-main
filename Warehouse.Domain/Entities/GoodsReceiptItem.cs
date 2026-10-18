using Warehouse.Domain.Common;

namespace Warehouse.Domain.Entities;

public sealed class GoodsReceiptItem : Entity<Guid>
{
    private GoodsReceiptItem()
    {
    }

    internal GoodsReceiptItem(
        Guid goodsReceiptId,
        Guid productId,
        decimal quantity,
        string? requestNumber,
        string? description)
    {
        Id = Guid.NewGuid();
        GoodsReceiptId = goodsReceiptId;
        ProductId = productId;
        Quantity = quantity;
        RequestNumber = Normalize(requestNumber);
        Description = Normalize(description);
    }

    public Guid GoodsReceiptId { get; private set; }

    public GoodsReceipt GoodsReceipt { get; private set; } = null!;

    public Guid ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    public decimal Quantity { get; private set; }

    public string? RequestNumber { get; private set; }

    public string? Description { get; private set; }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
