using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.DeleteGoodsReceipt;
using Warehouse.Application.Features.Inventory.WarehouseStocks;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.IntegrationTests.Persistence;

public sealed class WarehouseStockManagementTests(ReceiptInventoryDatabase database) : IClassFixture<ReceiptInventoryDatabase>
{
    private Task<Result> Open(ReceiptInventorySeed seed, decimal quantity, Guid? warehouse = null) =>
        database.Send(new RegisterOpeningBalancesCommand(warehouse ?? seed.A, [new(seed.Product, quantity)]));
    private async Task<WarehouseStockRow> Row(ReceiptInventorySeed seed, Guid? warehouse = null)
    {
        var result = await database.Send(new GetWarehouseStocksQuery { WarehouseId = warehouse ?? seed.A, ProductId = seed.Product });
        Assert.True(result.IsSuccess, string.Join(" ", result.Messages));
        return Assert.Single(result.Data!.Items);
    }
    private Task<Result> Adjust(WarehouseStockRow row, decimal quantity, string reason = "مغایرت شمارش فیزیکی") =>
        database.Send(new AdjustWarehouseStockCommand(row.WarehouseId, row.ProductId, quantity, row.CurrentQuantity, row.RowVersion, reason));

    [Fact]
    public async Task WarehousesAreIndependent_AndMissingBalanceIsZero()
    {
        var seed = await database.Seed();
        var empty = await Row(seed);
        Assert.Equal(0, empty.CurrentQuantity);
        Assert.Null(empty.OpeningQuantity);
        Assert.Null(empty.RowVersion);
        Assert.True((await Open(seed, 100)).IsSuccess);
        Assert.True((await Open(seed, 25, seed.B)).IsSuccess);
        var a = await Row(seed);
        var b = await Row(seed, seed.B);
        Assert.Equal(100, a.CurrentQuantity);
        Assert.Equal(100, a.OpeningQuantity);
        Assert.Equal(25, b.CurrentQuantity);
        Assert.NotNull(a.RowVersion);
    }

    [Fact]
    public async Task SearchFilterPaginationAndOrdering_AreAppliedOnServer()
    {
        var seed = await database.Seed();
        var marker = "STOCK-" + Guid.NewGuid().ToString("N");
        await database.Read(async db =>
        {
            var product = await db.Products.FindAsync(seed.Product);
            product!.Update(marker + " A", marker + "-001", product.UnitOfMeasureId, null);
            db.Add(new Product(marker + " B", marker + "-002", product.UnitOfMeasureId, null));
            return await db.SaveChangesAsync();
        });
        Assert.True((await Open(seed, 10)).IsSuccess);
        var page = (await database.Send(new GetWarehouseStocksQuery
        { WarehouseId = seed.A, Search = marker, PageSize = 1, PageNumber = 2, SortBy = "productCode" })).Data!;
        Assert.Equal(2, page.FilteredCount);
        Assert.EndsWith("-002", Assert.Single(page.Items).ProductCode);
        var zero = (await database.Send(new GetWarehouseStocksQuery { WarehouseId = seed.A, Search = marker, HasStock = false })).Data!;
        Assert.EndsWith("-002", Assert.Single(zero.Items).ProductCode);
        var positive = (await database.Send(new GetWarehouseStocksQuery { WarehouseId = seed.A, Search = marker, HasStock = true })).Data!;
        Assert.Equal(seed.Product, Assert.Single(positive.Items).ProductId);
    }

    [Fact]
    public async Task OpeningBatch_IsAtomic_AndDuplicatesAreRejected()
    {
        var first = await database.Seed();
        var second = await database.Seed();
        var batch = new RegisterOpeningBalancesCommand(first.A, [new(first.Product, 100), new(second.Product, 25)]);
        Assert.True((await database.Send(batch)).IsSuccess);
        Assert.Equal(100, await database.Quantity(first.A, first.Product));
        Assert.Equal(25, await database.Quantity(first.A, second.Product));
        Assert.False((await database.Send(batch)).IsSuccess);
        Assert.Equal(100, await database.Quantity(first.A, first.Product));
        var third = await database.Seed();
        var invalidBatch = new RegisterOpeningBalancesCommand(first.A, [new(third.Product, 50), new(second.Product, 25)]);
        Assert.False((await database.Send(invalidBatch)).IsSuccess);
        Assert.Equal(0, await database.Quantity(first.A, third.Product));
    }

    [Fact]
    public async Task OpeningAfterOperationalHistory_IsRejected_EvenWhenBalanceIsZero()
    {
        var seed = await database.Seed();
        var receipt = (await database.Send(seed.Create(seed.A, 5))).Data!;
        Assert.True((await database.Send(new DeleteGoodsReceiptCommand(receipt.Id))).IsSuccess);
        Assert.Equal(0, await database.Quantity(seed.A, seed.Product));
        Assert.False((await Open(seed, 100)).IsSuccess);
    }

    [Fact]
    public async Task ZeroOpening_IsIgnored_AndDoesNotReserveTheProduct()
    {
        var seed = await database.Seed();
        Assert.True((await Open(seed, 0)).IsSuccess);
        var row = await Row(seed);
        Assert.Null(row.RowVersion);
        Assert.Null(row.OpeningQuantity);
        Assert.True((await Open(seed, 5)).IsSuccess);
    }

    [Theory]
    [InlineData(95, -5)]
    [InlineData(110, 10)]
    public async Task Adjustment_UsesActualQuantity_AndPreservesReasonAndOpening(decimal target, decimal delta)
    {
        var seed = await database.Seed();
        await Open(seed, 100);
        Assert.True((await Adjust(await Row(seed), target)).IsSuccess);
        Assert.Equal(target, await database.Quantity(seed.A, seed.Product));
        var movement = await database.Read(db => db.StockMovements.AsNoTracking().SingleAsync(m =>
            m.WarehouseId == seed.A && m.ProductId == seed.Product && m.MovementType == InventoryMovementType.Adjustment));
        Assert.Equal(delta, movement.QuantityDelta);
        Assert.Equal("مغایرت شمارش فیزیکی", movement.Description);
        Assert.Equal(100, (await Row(seed)).OpeningQuantity);
    }

    [Fact]
    public async Task NoChangeAdjustment_DoesNotCreateMovement()
    {
        var seed = await database.Seed();
        await Open(seed, 100);
        Assert.True((await Adjust(await Row(seed), 100)).IsSuccess);
        Assert.Equal(1, await database.Read(db => db.StockMovements.CountAsync(m => m.WarehouseId == seed.A && m.ProductId == seed.Product)));
    }

    [Fact]
    public async Task InvalidValuesDuplicateProductsAndMissingReason_AreRejected()
    {
        var seed = await database.Seed();
        await Assert.ThrowsAsync<ValidationException>(() => Open(seed, -1));
        await Assert.ThrowsAsync<ValidationException>(() => database.Send(new RegisterOpeningBalancesCommand(seed.A, [new(seed.Product, 1), new(seed.Product, 2)])));
        var row = await Row(seed);
        await Assert.ThrowsAsync<ValidationException>(() => Adjust(row, -1));
        await Assert.ThrowsAsync<ValidationException>(() => Adjust(row, 1, " "));
        Assert.Equal(0, await database.Quantity(seed.A, seed.Product));
    }

    [Fact]
    public async Task StaleQuantityAndVersion_AreRejected_AndFirstBalanceCreationIsDetected()
    {
        var seed = await database.Seed();
        var missing = await Row(seed);
        await Open(seed, 100);
        Assert.False((await Adjust(missing, 10)).IsSuccess);
        var old = await Row(seed);
        await Adjust(old, 95);
        Assert.False((await Adjust(old, 110)).IsSuccess);
        var current = await Row(seed);
        Assert.False((await Adjust(current with { RowVersion = new byte[8] }, 100)).IsSuccess);
        Assert.False((await Adjust(current with { CurrentQuantity = 99 }, 100)).IsSuccess);
        Assert.Equal(95, await database.Quantity(seed.A, seed.Product));
    }

    [Fact]
    public async Task ConcurrentOpenings_OnlyOneCanApply()
    {
        var seed = await database.Seed();
        var outcomes = await Task.WhenAll(Try(() => Open(seed, 100)), Try(() => Open(seed, 100)));
        Assert.Single(outcomes, success => success);
        Assert.Equal(100, await database.Quantity(seed.A, seed.Product));
        Assert.Equal(1, await database.Read(db => db.StockMovements.CountAsync(m => m.WarehouseId == seed.A && m.ProductId == seed.Product)));
    }

    [Fact]
    public async Task ConcurrentFirstAdjustments_OnlyOneCreatesTheBalance()
    {
        var seed = await database.Seed();
        var missing = await Row(seed);
        var outcomes = await Task.WhenAll(Try(() => Adjust(missing, 10)), Try(() => Adjust(missing, 20)));
        Assert.Single(outcomes, success => success);
        Assert.Contains(await database.Quantity(seed.A, seed.Product), new[] { 10m, 20m });
    }

    [Fact]
    public async Task AdjustmentRacingWithConsumption_NeverLosesAnAppliedMovement()
    {
        var seed = await database.Seed();
        await Open(seed, 100);
        var row = await Row(seed);
        await Task.WhenAll(Try(() => Adjust(row, 95)), Try(async () =>
        {
            await database.Consume(seed, 20);
            return Result.Success();
        }));
        var quantity = await database.Quantity(seed.A, seed.Product);
        var ledger = await database.Read(db => db.StockMovements.Where(m => m.WarehouseId == seed.A && m.ProductId == seed.Product).SumAsync(m => m.QuantityDelta));
        Assert.Equal(ledger, quantity);
        Assert.Contains(quantity, new[] { 75m, 80m, 95m });
    }

    private static async Task<bool> Try(Func<Task<Result>> action)
    {
        try { return (await action()).IsSuccess; }
        catch (Exception error) when (IsConflict(error)) { return false; }
    }
    private static bool IsConflict(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException or SqlException { Number: 1205 }) return true;
        return false;
    }
}
