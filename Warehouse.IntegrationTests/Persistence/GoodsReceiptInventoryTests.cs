using ClosedXML.Excel;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Warehouse.Application;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.DeleteGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.ImportGoodsReceipts;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.UpdateGoodsReceipt;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;
using Warehouse.Infrastructure;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.IntegrationTests.Persistence;

public sealed class GoodsReceiptInventoryTests(ReceiptInventoryDatabase database) : IClassFixture<ReceiptInventoryDatabase>
{
    [Fact]
    public async Task UnassignedReceipt_QuantityEditsAndDeletion_DoNotAffectStock()
    {
        var seed = await database.Seed();
        var receipt = (await database.Send(seed.Create(null, 20))).Data!;
        Assert.Null(receipt.Items.Single().WarehouseId);
        Assert.Equal(string.Empty, receipt.Items.Single().WarehouseTitle);
        receipt = (await database.Send(seed.Edit(receipt, null, 30))).Data!;
        Assert.Equal(30, receipt.Items.Single().Quantity);
        Assert.Empty(await database.Movements(receipt.Id));
        Assert.Equal(0, await database.Quantity(seed.A, seed.Product));
        Assert.True((await database.Send(new DeleteGoodsReceiptCommand(receipt.Id))).IsSuccess);
        Assert.Empty(await database.Movements(receipt.Id));
    }

    [Fact]
    public async Task FirstAssignment_UsesCurrentQuantityAndActualTime_AndIsIdempotent()
    {
        var seed = await database.Seed();
        var receipt = (await database.Send(seed.Create(null, 20))).Data!;
        receipt = (await database.Send(seed.Edit(receipt, null, 30))).Data!;
        receipt = (await database.Send(seed.Edit(receipt, seed.A, 30))).Data!;
        var history = await database.Movements(receipt.Id);
        Assert.Equal(30, Assert.Single(history).QuantityDelta);
        Assert.Equal(ReceiptInventoryDatabase.Now, history[0].OccurredAtUtc);
        Assert.Equal(ReceiptInventoryDatabase.Now, history[0].CreatedOn);
        Assert.True(history[0].OccurredAtUtc > receipt.EnteredAtUtc);
        receipt = (await database.Send(seed.Edit(receipt, seed.A, 30))).Data!;
        Assert.Single(await database.Movements(receipt.Id));
        Assert.Equal(30, await database.Quantity(seed.A, seed.Product));
    }

    [Fact]
    public async Task AssignedQuantityEdits_ApplyOnlyDifferences_AndKeepOriginalEntry()
    {
        var seed = await database.Seed();
        var receipt = (await database.Send(seed.Create(seed.A, 20))).Data!;
        var original = Assert.Single(await database.Movements(receipt.Id));
        receipt = (await database.Send(seed.Edit(receipt, seed.A, 25))).Data!;
        Assert.Equal(25, await database.Quantity(seed.A, seed.Product));
        Assert.Contains(await database.Movements(receipt.Id), m => m.QuantityDelta == 5);
        receipt = (await database.Send(seed.Edit(receipt, seed.A, 22))).Data!;
        var history = await database.Movements(receipt.Id);
        Assert.Contains(history, m => m.Id == original.Id && m.QuantityDelta == 20);
        Assert.Contains(history, m => m.QuantityDelta == -3);
        Assert.Equal(22, await database.Quantity(seed.A, seed.Product));
        Assert.True((await database.Send(new DeleteGoodsReceiptCommand(receipt.Id))).IsSuccess);
        Assert.Equal(0, await database.Quantity(seed.A, seed.Product));
        Assert.Equal(0, (await database.Movements(receipt.Id)).Sum(m => m.QuantityDelta));
    }

    [Fact]
    public async Task WarehouseChange_ClearAndReassignment_KeepHistoryAndCorrectBalances()
    {
        var seed = await database.Seed();
        var receipt = (await database.Send(seed.Create(seed.A, 20))).Data!;
        receipt = (await database.Send(seed.Edit(receipt, seed.B, 20))).Data!;
        Assert.Equal(0, await database.Quantity(seed.A, seed.Product));
        Assert.Equal(20, await database.Quantity(seed.B, seed.Product));
        var original = (await database.Movements(receipt.Id)).Single(m => m.WarehouseId == seed.A && m.QuantityDelta == 20);
        Assert.Contains(await database.Movements(receipt.Id), m => m.ReversedMovementId == original.Id && m.QuantityDelta == -20);
        // Warehouse-only permission must be able to clear it explicitly.
        var clear = seed.Edit(receipt, null, 20) with { CanEditInitialEntry = false };
        receipt = (await database.Send(clear)).Data!;
        Assert.Null(receipt.Items.Single().WarehouseId);
        Assert.Equal(0, await database.Quantity(seed.B, seed.Product));
        receipt = (await database.Send(seed.Edit(receipt, seed.A, 20))).Data!;
        Assert.Equal(20, await database.Quantity(seed.A, seed.Product));
    }

    [Theory]
    [InlineData("move")]
    [InlineData("clear")]
    [InlineData("delete")]
    [InlineData("reduce")]
    public async Task InsufficientStock_RejectsEntireChange(string action)
    {
        var seed = await database.Seed();
        var receipt = (await database.Send(seed.Create(seed.A, 20))).Data!;
        await database.Consume(seed, 15);
        var before = await database.Movements(receipt.Id);
        Result result = action == "delete"
            ? await database.Send(new DeleteGoodsReceiptCommand(receipt.Id))
            : await database.Send(seed.Edit(receipt, action == "move" ? seed.B : action == "clear" ? null : seed.A,
                action == "reduce" ? 10 : 20));
        Assert.False(result.IsSuccess);
        Assert.Equal(5, await database.Quantity(seed.A, seed.Product));
        Assert.Equal(0, await database.Quantity(seed.B, seed.Product));
        Assert.Equal(before.Select(m => m.Id), (await database.Movements(receipt.Id)).Select(m => m.Id));
        await database.Read(async db =>
        {
            var persisted = await db.GoodsReceiptItems.SingleAsync(item => item.GoodsReceiptId == receipt.Id);
            Assert.Equal(seed.A, persisted.WarehouseId);
            Assert.Equal(20, persisted.Quantity);
            Assert.Equal(receipt.ConcurrencyStamp, (await db.GoodsReceipts.FindAsync(receipt.Id))!.ConcurrencyStamp);
            return true;
        });
    }

    [Fact]
    public async Task HistoricalUnassignedReceipt_IsNotGuessed_AndCanBeAssignedLater()
    {
        var seed = await database.Seed();
        var id = await database.Read(async db =>
        {
            var receipt = new GoodsReceipt(ReceiptInventoryDatabase.Now.AddDays(-10), null, seed.Person, Guid.NewGuid().ToString());
            receipt.AddItem(seed.Product, null, null, null, null, 12, null, null, 0);
            db.Add(receipt);
            await db.SaveChangesAsync();
            return receipt.Id;
        });
        var receipt = (await database.Send(new Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptById.GetGoodsReceiptByIdQuery(id))).Data!;
        Assert.Null(receipt.Items.Single().WarehouseId);
        Assert.Empty(await database.Movements(id));
        Assert.True((await database.Send(seed.Edit(receipt, seed.A, 12))).IsSuccess);
        Assert.Equal(12, await database.Quantity(seed.A, seed.Product));
    }

    [Fact]
    public async Task ConcurrentAssignment_OnlyOneEditCanApplyStock()
    {
        var seed = await database.Seed();
        var receipt = (await database.Send(seed.Create(null, 20))).Data!;
        var command = seed.Edit(receipt, seed.A, 20);
        async Task<bool> Attempt()
        {
            try { return (await database.Send(command)).IsSuccess; }
            catch (DbUpdateConcurrencyException) { return false; }
            catch (SqlException e) when (e.Number == 1205) { return false; }
            catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 1205 }) { return false; }
            catch (InvalidOperationException e) when (e.InnerException?.InnerException is SqlException { Number: 1205 }) { return false; }
        }
        var outcomes = await Task.WhenAll(Attempt(), Attempt());
        Assert.Single(outcomes, success => success);
        Assert.Equal(20, await database.Quantity(seed.A, seed.Product));
        Assert.Single(await database.Movements(receipt.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Import_AllowsBlankWarehouse_AndAccumulatesAssignedRows(bool assigned)
    {
        var seed = await database.Seed();
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("ورود کالا");
        sheet.Cell(1, 1).Value = "ردیف";
        for (var row = 2; row <= 3; row++)
        {
            sheet.Cell(row, 2).Value = "2026/11/01";
            sheet.Cell(row, 4).Value = seed.ProductTitle;
            sheet.Cell(row, 5).Value = seed.Code;
            sheet.Cell(row, 6).Value = 7;
            sheet.Cell(row, 7).Value = seed.UnitTitle;
            sheet.Cell(row, 9).Value = seed.PersonTitle;
            sheet.Cell(row, 14).Value = assigned ? seed.WarehouseTitle : "";
            sheet.Cell(row, 15).Value = Guid.NewGuid().ToString();
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var result = await database.Send(new ImportGoodsReceiptsCommand("receipts.xlsx", stream.ToArray(), true, true));
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.InvalidRows);
        Assert.Equal(2, result.Data.ImportedReceiptCount);
        Assert.Equal(assigned ? 14 : 0, await database.Quantity(seed.A, seed.Product));
    }

    [Fact]
    public async Task MixedRows_AreAssignedIndependently_AndRemovedRowsAreReversed()
    {
        var seed = await database.Seed();
        var command = seed.Create(seed.A, 20);
        command = command with { Items = [command.Items.Single(), command.Items.Single() with { WarehouseId = null, Quantity = 7 }] };
        var receipt = (await database.Send(command)).Data!;
        Assert.Equal(20, await database.Quantity(seed.A, seed.Product));
        Assert.Single(await database.Movements(receipt.Id));
        var rows = receipt.Items.OrderBy(item => item.Quantity).ToArray();
        var assigned = command.Items.Single(item => item.Quantity == 20) with { Id = rows[1].Id };
        var newlyAssigned = command.Items.Single(item => item.Quantity == 7) with { Id = rows[0].Id, WarehouseId = seed.B, Quantity = 11 };
        receipt = (await database.Send(new UpdateGoodsReceiptCommand(receipt.Id,
            command with { Items = [assigned, newlyAssigned] }, true, true, true, receipt.ConcurrencyStamp, true))).Data!;
        Assert.Equal(20, await database.Quantity(seed.A, seed.Product));
        Assert.Equal(11, await database.Quantity(seed.B, seed.Product));
        var result = await database.Send(new UpdateGoodsReceiptCommand(receipt.Id,
            command with { Items = [newlyAssigned] }, true, true, true, receipt.ConcurrencyStamp, true));
        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
        Assert.Equal(0, await database.Quantity(seed.A, seed.Product));
        Assert.Equal(11, await database.Quantity(seed.B, seed.Product));
    }

    [Fact]
    public async Task HistoricalMovements_CannotBeUpdatedOrDeleted()
    {
        var seed = await database.Seed();
        var receipt = (await database.Send(seed.Create(seed.A, 1))).Data!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => database.Read(async db =>
        {
            db.StockMovements.Remove(await db.StockMovements.SingleAsync(m => m.SourceDocumentId == receipt.Id));
            return await db.SaveChangesAsync();
        }));
        Assert.Single(await database.Movements(receipt.Id));
    }
}

public sealed record ReceiptInventorySeed(Guid Product, Guid Person, Guid A, Guid B,
    string ProductTitle, string Code, string UnitTitle, string PersonTitle, string WarehouseTitle)
{
    public CreateGoodsReceiptCommand Create(Guid? warehouse, decimal quantity) =>
        new(ReceiptInventoryDatabase.Now.AddDays(-2), null, Person, null, Guid.NewGuid().ToString(),
            [new(Product, null, null, null, warehouse, null, null, null, null, null, quantity, null, null)]);
    public UpdateGoodsReceiptCommand Edit(GoodsReceiptSummary receipt, Guid? warehouse, decimal quantity) =>
        new(receipt.Id, new(receipt.EnteredAtUtc, receipt.Description, Person, null, receipt.PageNumber,
            [new(Product, null, null, null, warehouse, null, null, null, null, null, quantity, null, null,
                Id: receipt.Items.Single().Id)]), true, true, true, receipt.ConcurrencyStamp, true);
}

public sealed class ReceiptInventoryDatabase : IAsyncLifetime
{
    public static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private ServiceProvider _services = null!;
    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        var connection = $"Server=(localdb)\\MSSQLLocalDB;Database=Warehouse_ReceiptInventory_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True";
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connection }).Build());
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure();
        services.AddSingleton<TimeProvider>(new FixedClock());
        _services = services.BuildServiceProvider();
        await Read(async db => { await db.Database.MigrateAsync(); return true; });
    }
    public async Task<T> Read<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        await using var scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }
    public async Task<T> Send<T>(IRequest<T> request)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
    public Task<ReceiptInventorySeed> Seed() => Read(async db =>
    {
        var suffix = Guid.NewGuid().ToString("N");
        var unit = new UnitOfMeasure("Unit-" + suffix, null);
        var product = new Product("Product-" + suffix, suffix, unit.Id, null);
        var person = new Person("Person-" + suffix, "");
        var a = new StorageWarehouse("A-" + suffix, null);
        var b = new StorageWarehouse("B-" + suffix, null);
        db.AddRange(unit, product, person, a, b);
        await db.SaveChangesAsync();
        return new ReceiptInventorySeed(product.Id, person.Id, a.Id, b.Id, product.Title, product.Code, unit.Title, person.FullName, a.Title);
    });
    public Task<decimal> Quantity(Guid warehouse, Guid product) => Read(async db =>
        await db.WarehouseStockBalances.Where(stock => stock.WarehouseId == warehouse && stock.ProductId == product)
            .Select(stock => (decimal?)stock.Quantity).SingleOrDefaultAsync() ?? 0);
    public Task<List<StockMovement>> Movements(Guid receipt) => Read(db => db.StockMovements.AsNoTracking()
        .Where(m => m.SourceDocumentId == receipt).OrderBy(m => m.Id).ToListAsync());
    public Task<int> Consume(ReceiptInventorySeed seed, decimal quantity) => Read(async db =>
    {
        var stock = await db.WarehouseStockBalances.SingleAsync(s => s.WarehouseId == seed.A && s.ProductId == seed.Product);
        stock.ApplyDelta(-quantity);
        db.Add(new StockMovement(seed.A, seed.Product, -quantity, InventoryMovementType.GoodsIssue, Guid.NewGuid(), Now));
        return await db.SaveChangesAsync();
    });
    public async Task DisposeAsync()
    {
        await Read(db => db.Database.EnsureDeletedAsync());
        await _services.DisposeAsync();
    }
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
