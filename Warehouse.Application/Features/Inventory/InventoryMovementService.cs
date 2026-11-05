using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;
using Warehouse.Application.Common.Concurrency;

namespace Warehouse.Application.Features.Inventory;

public sealed class InventoryMovementService(IUnitOfWork<Guid> unitOfWork, TimeProvider clock)
    : IInventoryMovementService
{
    private readonly Dictionary<(Guid Warehouse, Guid Product), WarehouseStockBalance> _balances = [];
    private readonly Dictionary<Guid, List<StockMovement>> _history = [];

    public async Task<Result> RegisterOpeningBalancesAsync(Guid warehouseId,
        IReadOnlyCollection<OpeningStockItem> items, CancellationToken cancellationToken)
    {
        var ids = items.Select(item => item.ProductId).ToArray();
        var products = unitOfWork.Repository<Product>();
        if (await products.CountAsync(products.Entities.Where(product => ids.Contains(product.Id)), cancellationToken) != ids.Length)
            return Result.Error("یک یا چند کالای انتخاب‌شده پیدا نشد.");
        var movements = unitOfWork.Repository<StockMovement>();
        if (await movements.CountAsync(movements.Entities.Where(movement =>
                movement.WarehouseId == warehouseId && ids.Contains(movement.ProductId)), cancellationToken) > 0)
            return Result.Error("یک یا چند کالا قبلاً موجودی اولیه یا گردش موجودی داشته‌اند؛ از اصلاح موجودی استفاده کنید.");

        var pending = new List<(WarehouseStockBalance Balance, decimal Quantity)>();
        foreach (var item in items.OrderBy(item => item.ProductId))
        {
            var balance = await LoadBalanceAsync(warehouseId, item.ProductId, cancellationToken);
            if (balance?.Quantity > 0)
                return Result.Error("این کالا دارای موجودی است؛ از اصلاح موجودی استفاده کنید.");
            if (item.Quantity == 0) continue; // Zero does not reserve an opening balance or create any row.
            balance ??= await AddBalanceAsync(warehouseId, item.ProductId, cancellationToken);
            pending.Add((balance, item.Quantity));
        }
        foreach (var (balance, quantity) in pending)
        {
            await movements.AddAsync(new StockMovement(warehouseId, balance.ProductId, quantity,
                InventoryMovementType.OpeningBalance, Guid.NewGuid(), clock.GetUtcNow()), cancellationToken);
            balance.ApplyDelta(quantity);
        }
        return Result.Success(pending.Count == 0 ? "مقادیر صفر نادیده گرفته شدند؛ موجودی تغییری نکرد." : "موجودی اولیه با موفقیت ثبت شد.");
    }

    public async Task<Result> AdjustStockAsync(Guid warehouseId, Guid productId, decimal newQuantity,
        decimal expectedQuantity, byte[]? rowVersion, string reason, CancellationToken cancellationToken)
    {
        var products = unitOfWork.Repository<Product>();
        if (await products.CountAsync(products.Entities.Where(product => product.Id == productId), cancellationToken) != 1)
            return Result.Error("کالای انتخاب‌شده پیدا نشد.");
        var balance = await LoadBalanceAsync(warehouseId, productId, cancellationToken);
        if ((balance?.Quantity ?? 0) != expectedQuantity ||
            (balance is null ? rowVersion is not null : rowVersion is null || !balance.RowVersion.SequenceEqual(rowVersion)))
            return Result.Error(ConcurrencyMessages.Conflict);
        var delta = newQuantity - (balance?.Quantity ?? 0);
        if (delta == 0) return Result.Success("موجودی تغییری نکرد؛ گردش جدیدی ثبت نشد.");
        balance ??= await AddBalanceAsync(warehouseId, productId, cancellationToken);
        await unitOfWork.Repository<StockMovement>().AddAsync(new StockMovement(warehouseId, productId,
            delta, InventoryMovementType.Adjustment, Guid.NewGuid(), clock.GetUtcNow(), description: reason), cancellationToken);
        balance.ApplyDelta(delta);
        return Result.Success("موجودی کالا با موفقیت اصلاح شد.");
    }

    private async Task<WarehouseStockBalance?> LoadBalanceAsync(Guid warehouseId, Guid productId, CancellationToken token)
    {
        if (_balances.TryGetValue((warehouseId, productId), out var cached)) return cached;
        var repository = unitOfWork.Repository<WarehouseStockBalance>();
        var stored = await repository.ToListAsync(repository.Entities.Where(balance =>
            balance.WarehouseId == warehouseId && balance.ProductId == productId), token);
        if (stored.Count == 0) return null;
        var balance = (await repository.GetByIdAsync(stored.Single().Id, token))!;
        _balances[(warehouseId, productId)] = balance;
        return balance;
    }

    private async Task<WarehouseStockBalance> AddBalanceAsync(Guid warehouseId, Guid productId, CancellationToken token)
    {
        var balance = new WarehouseStockBalance(warehouseId, productId);
        await unitOfWork.Repository<WarehouseStockBalance>().AddAsync(balance, token);
        _balances[(warehouseId, productId)] = balance;
        return balance;
    }

    public async Task<Result> SynchronizeGoodsReceiptAsync(Guid documentId,
        IReadOnlyCollection<GoodsReceiptItem> currentItems, CancellationToken cancellationToken)
    {
        var movements = unitOfWork.Repository<StockMovement>();
        if (!_history.TryGetValue(documentId, out var history))
        {
            history = (await movements.ToListAsync(movements.Entities.Where(movement =>
                movement.MovementType == InventoryMovementType.GoodsReceipt &&
                movement.SourceDocumentId == documentId), cancellationToken)).ToList();
            _history[documentId] = history;
        }

        // Never infer historical inventory from the old document's warehouse alone.
        var previous = history.GroupBy(m => (m.SourceLineId, m.WarehouseId, m.ProductId))
            .ToDictionary(group => group.Key, group => group.Sum(m => m.QuantityDelta));
        var desired = currentItems.Where(item => item.WarehouseId.HasValue)
            .ToDictionary(item => (SourceLineId: (Guid?)item.Id, WarehouseId: item.WarehouseId!.Value, ProductId: item.ProductId), item => item.Quantity);
        var changes = previous.Keys.Union(desired.Keys).Select(key =>
            (Key: key, Delta: desired.GetValueOrDefault(key) - previous.GetValueOrDefault(key)))
            .Where(change => change.Delta != 0).ToArray();
        if (changes.Length == 0) return Result.Success();

        var pending = new List<(WarehouseStockBalance Balance, decimal Delta)>();
        // Stable lock order reduces contention between documents touching the same warehouses.
        foreach (var group in changes.GroupBy(change => (Warehouse: change.Key.WarehouseId, Product: change.Key.ProductId))
                     .OrderBy(group => group.Key.Warehouse).ThenBy(group => group.Key.Product))
        {
            var balance = await LoadBalanceAsync(group.Key.Warehouse, group.Key.Product, cancellationToken)
                ?? await AddBalanceAsync(group.Key.Warehouse, group.Key.Product, cancellationToken);
            var delta = group.Sum(change => change.Delta);
            if (balance.Quantity + delta < 0)
                return Result.Error("ویرایش یا حذف سند باعث منفی شدن موجودی انبار می‌شود.");
            pending.Add((balance, delta));
        }

        // Occurrence is the actual application time, never the backdated receipt date.
        var now = clock.GetUtcNow();
        foreach (var change in changes)
        {
            var key = change.Key;
            var prior = history.LastOrDefault(m => m.SourceLineId == key.SourceLineId &&
                m.WarehouseId == key.WarehouseId && m.ProductId == key.ProductId &&
                !history.Any(reversal => reversal.ReversedMovementId == m.Id));
            var operationId = Guid.NewGuid();
            var entry = prior is null
                ? new StockMovement(key.WarehouseId, key.ProductId, change.Delta,
                    InventoryMovementType.GoodsReceipt, operationId, now, documentId, key.SourceLineId)
                : change.Delta == -prior.QuantityDelta
                    ? prior.CreateReversal(operationId, now, "اصلاح اثر سند ورود کالا")
                    : prior.CreateCorrection(change.Delta, operationId, now, "اصلاح اثر سند ورود کالا");
            await movements.AddAsync(entry, cancellationToken);
            history.Add(entry);
        }
        foreach (var (balance, delta) in pending) balance.ApplyDelta(delta);
        return Result.Success();
    }
}
