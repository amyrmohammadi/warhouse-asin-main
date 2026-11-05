using Warehouse.Domain.Common;

namespace Warehouse.Domain.Entities;

public sealed class DepartmentReceiptItem : Entity<Guid>
{
    private DepartmentReceiptItem() { }

    internal DepartmentReceiptItem(DepartmentReceipt receipt, Guid productId, decimal quantity,
        string? assetNumber, string? description)
    {
        Id = Guid.NewGuid();
        DepartmentReceiptId = receipt.Id;
        DepartmentReceipt = receipt;
        Update(productId, quantity, assetNumber, description);
    }

    public Guid DepartmentReceiptId { get; private set; }
    public DepartmentReceipt DepartmentReceipt { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public string? AssetNumber { get; private set; }
    public string? Description { get; private set; }

    internal void Update(Guid productId, decimal quantity, string? assetNumber, string? description)
    {
        if (productId == Guid.Empty) throw new ArgumentException("Product is required.", nameof(productId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ProductId = productId;
        Quantity = quantity;
        AssetNumber = string.IsNullOrWhiteSpace(assetNumber) ? null : assetNumber.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
