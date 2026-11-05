using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Common.Interfaces;

public interface IInventoryMovementService
{
    // Caller owns a serializable transaction and saves the document, entries and balances together.
    Task<Result> SynchronizeGoodsReceiptAsync(Guid documentId,
        IReadOnlyCollection<GoodsReceiptItem> currentItems, CancellationToken cancellationToken);

    Task<Result> RegisterOpeningBalancesAsync(Guid warehouseId,
        IReadOnlyCollection<OpeningStockItem> items, CancellationToken cancellationToken);

    Task<Result> AdjustStockAsync(Guid warehouseId, Guid productId, decimal newQuantity,
        decimal expectedQuantity, byte[]? rowVersion, string reason, CancellationToken cancellationToken);
}

public sealed record OpeningStockItem(Guid ProductId, decimal Quantity);
