using Warehouse.Domain.Common;
using Warehouse.Domain.Enums;

namespace Warehouse.Domain.Entities;

/// <summary>
/// An append-only inventory entry. Corrections create new entries instead of changing history.
/// OperationId is supplied by the caller and identifies one logical entry across retries;
/// persistence must enforce its uniqueness. Audit fields come from Entity.
/// </summary>
public sealed class StockMovement : Entity<Guid>
{
    private StockMovement() { }

    public StockMovement(Guid warehouseId, Guid productId, decimal quantityDelta,
        InventoryMovementType movementType, Guid operationId, DateTimeOffset occurredAtUtc,
        Guid? sourceDocumentId = null, Guid? sourceLineId = null, string? description = null)
        : this(warehouseId, productId, quantityDelta, movementType, operationId, occurredAtUtc,
            sourceDocumentId, sourceLineId, description, null)
    {
    }

    private StockMovement(Guid warehouseId, Guid productId, decimal quantityDelta,
        InventoryMovementType movementType, Guid operationId, DateTimeOffset occurredAtUtc,
        Guid? sourceDocumentId, Guid? sourceLineId, string? description, Guid? reversedMovementId,
        bool isCorrection = false)
    {
        if (warehouseId == Guid.Empty) throw new ArgumentException("Warehouse is required.", nameof(warehouseId));
        if (productId == Guid.Empty) throw new ArgumentException("Product is required.", nameof(productId));
        if (operationId == Guid.Empty) throw new ArgumentException("Operation is required.", nameof(operationId));
        if (sourceDocumentId == Guid.Empty) throw new ArgumentException("Source document must be specified or null.", nameof(sourceDocumentId));
        if (sourceLineId == Guid.Empty) throw new ArgumentException("Source line must be specified or null.", nameof(sourceLineId));
        if (!Enum.IsDefined(movementType)) throw new ArgumentOutOfRangeException(nameof(movementType));
        if (quantityDelta == 0) throw new ArgumentOutOfRangeException(nameof(quantityDelta), "A movement cannot have zero quantity.");
        if (movementType == InventoryMovementType.Adjustment)
            ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (reversedMovementId is null && !isCorrection)
        {
            var mustIncrease = movementType is InventoryMovementType.GoodsReceipt
                or InventoryMovementType.DepartmentReceipt or InventoryMovementType.OpeningBalance;
            var mustDecrease = movementType is InventoryMovementType.GoodsIssue
                or InventoryMovementType.DepartmentDelivery;
            if ((mustIncrease && quantityDelta < 0) || (mustDecrease && quantityDelta > 0))
                throw new ArgumentOutOfRangeException(nameof(quantityDelta), "Quantity direction does not match the movement type.");
        }

        Id = Guid.NewGuid();
        WarehouseId = warehouseId;
        ProductId = productId;
        QuantityDelta = quantityDelta;
        MovementType = movementType;
        OperationId = operationId;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        SourceDocumentId = sourceDocumentId;
        SourceLineId = sourceLineId;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        ReversedMovementId = reversedMovementId;
    }

    public Guid WarehouseId { get; private set; }
    public StorageWarehouse Warehouse { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public decimal QuantityDelta { get; private set; }
    public InventoryMovementType MovementType { get; private set; }
    public Guid OperationId { get; private set; }
    public Guid? SourceDocumentId { get; private set; }
    public Guid? SourceLineId { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string? Description { get; private set; }
    public Guid? ReversedMovementId { get; private set; }
    public StockMovement? ReversedMovement { get; private set; }

    /// <summary>An incremental correction retains the source document and line; history stays immutable.</summary>
    public StockMovement CreateCorrection(decimal quantityDelta, Guid operationId,
        DateTimeOffset occurredAtUtc, string? description)
    {
        if (operationId == OperationId)
            throw new ArgumentException("A correction needs a different operation identifier.", nameof(operationId));
        return new StockMovement(WarehouseId, ProductId, quantityDelta, MovementType, operationId,
            occurredAtUtc, SourceDocumentId, SourceLineId, description, null, true);
    }

    /// <summary>
    /// Describes the inverse of this entry without mutating it or applying any stock changes.
    /// The application must prevent duplicate reversals and save movement/balance changes atomically.
    /// </summary>
    public StockMovement CreateReversal(Guid operationId, DateTimeOffset occurredAtUtc, string? description = null)
    {
        if (operationId == OperationId)
            throw new ArgumentException("A reversal needs a different operation identifier.", nameof(operationId));

        return new StockMovement(WarehouseId, ProductId, -QuantityDelta, MovementType, operationId,
            occurredAtUtc, SourceDocumentId, SourceLineId, description ?? Description, Id);
    }
}
