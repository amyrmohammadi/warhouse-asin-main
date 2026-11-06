using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.WarehouseStocks;
using Warehouse.Application.Tests.TestSupport;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory;

public sealed class StockHistoryTests
{
    [Fact]
    public async Task History_IsolatesProductAndWarehouseAndPaginatesNewestFirst()
    {
        await using var context = new ApplicationTestContext();
        var warehouse = Guid.NewGuid();
        var product = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var opening = new StockMovement(warehouse, product, 10, InventoryMovementType.OpeningBalance, Guid.NewGuid(), now);
        var issue = new StockMovement(warehouse, product, -3, InventoryMovementType.GoodsIssue, Guid.NewGuid(), now.AddMinutes(1));
        var reversal = issue.CreateReversal(Guid.NewGuid(), now.AddMinutes(2), "برگشت خروج");
        var repository = Substitute.For<IRepositoryAsync<StockMovement, Guid>>();
        repository.Entities.Returns(new[] {
            opening, issue, reversal,
            new StockMovement(Guid.NewGuid(), product, 50, InventoryMovementType.OpeningBalance, Guid.NewGuid(), now),
            new StockMovement(warehouse, Guid.NewGuid(), 60, InventoryMovementType.OpeningBalance, Guid.NewGuid(), now),
        }.AsQueryable());
        repository.CountAsync(Arg.Any<IQueryable<StockMovement>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<StockMovement>>().Count());
        repository.ToListAsync(Arg.Any<IQueryable<StockHistoryRow>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<StockHistoryRow>>().ToList());
        context.UnitOfWork.Repository<StockMovement>().Returns(repository);
        context.UnitOfWork.Repository<GoodsReceipt>().Entities.Returns(Array.Empty<GoodsReceipt>().AsQueryable());
        context.UnitOfWork.Repository<GoodsReceiptItem>().Entities.Returns(Array.Empty<GoodsReceiptItem>().AsQueryable());

        var first = await context.Sender.Send(new GetStockHistoryQuery(warehouse, product, 1, 2));
        Assert.Equal(3, first.Data!.TotalCount);
        Assert.Equal(new[] { reversal.Id, issue.Id }, first.Data.Items.Select(row => row.Id));
        Assert.True(first.Data.Items[0].IsReversal);
        Assert.Equal(3, first.Data.Items[0].QuantityDelta);
        Assert.Equal(-3, first.Data.Items[1].QuantityDelta);
        var second = await context.Sender.Send(new GetStockHistoryQuery(warehouse, product, 2, 2));
        Assert.Equal(opening.Id, Assert.Single(second.Data!.Items).Id);
    }

    [Theory]
    [InlineData("توضیحات قلم ورود")]
    [InlineData(null)]
    public async Task History_ReceiptUsesFactoryEntryDatePageAndItemDescription(string? description)
    {
        await using var context = new ApplicationTestContext();
        var warehouse = Guid.NewGuid();
        var product = Guid.NewGuid();
        var enteredAt = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        var receipt = new GoodsReceipt(enteredAt, "توضیحات سند", Guid.NewGuid(), "123");
        receipt.AddItem(product, warehouse, null, null, null, 5, null, description, 0);
        var item = Assert.Single(receipt.Items);
        var movement = new StockMovement(warehouse, product, 5, InventoryMovementType.GoodsReceipt,
            Guid.NewGuid(), enteredAt.AddDays(10), receipt.Id, item.Id, "توضیح گردش");
        var repository = Substitute.For<IRepositoryAsync<StockMovement, Guid>>();
        repository.Entities.Returns(new[] { movement }.AsQueryable());
        repository.CountAsync(Arg.Any<IQueryable<StockMovement>>(), Arg.Any<CancellationToken>()).Returns(1);
        repository.ToListAsync(Arg.Any<IQueryable<StockHistoryRow>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<StockHistoryRow>>().ToList());
        context.UnitOfWork.Repository<StockMovement>().Returns(repository);
        context.UnitOfWork.Repository<GoodsReceipt>().Entities.Returns(new[] { receipt }.AsQueryable());
        context.UnitOfWork.Repository<GoodsReceiptItem>().Entities.Returns(receipt.Items.AsQueryable());

        var result = await context.Sender.Send(new GetStockHistoryQuery(warehouse, product));

        var row = Assert.Single(result.Data!.Items);
        Assert.Equal(enteredAt, row.Date);
        Assert.Equal("123", row.PageNumber);
        Assert.Equal(description, row.Description);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 101)]
    public async Task History_RejectsInvalidPagination(int page, int size)
    {
        await using var context = new ApplicationTestContext();
        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(
            new GetStockHistoryQuery(Guid.NewGuid(), Guid.NewGuid(), page, size)));
    }
}
