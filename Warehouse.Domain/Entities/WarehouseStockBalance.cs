using Warehouse.Domain.Common;

namespace Warehouse.Domain.Entities;

/// <summary>
/// Current quantity for one product in one warehouse. Persistence must enforce
/// uniqueness of (WarehouseId, ProductId) and configure RowVersion as a concurrency token.
/// </summary>
public sealed class WarehouseStockBalance : Entity<Guid>
{
    private WarehouseStockBalance() { }

    public WarehouseStockBalance(Guid warehouseId, Guid productId, decimal quantity = 0)
    {
        if (warehouseId == Guid.Empty) throw new ArgumentException("Warehouse is required.", nameof(warehouseId));
        if (productId == Guid.Empty) throw new ArgumentException("Product is required.", nameof(productId));
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        Id = Guid.NewGuid();
        WarehouseId = warehouseId;
        ProductId = productId;
        Quantity = quantity;
    }

    public Guid WarehouseId { get; private set; }
    public StorageWarehouse Warehouse { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void ApplyDelta(decimal quantityDelta)
    {
        var next = checked(Quantity + quantityDelta);
        if (next < 0) throw new InvalidOperationException("موجودی انبار نمی‌تواند منفی شود.");
        Quantity = next;
    }
}
