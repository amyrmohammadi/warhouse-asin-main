using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptMetrics;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsReceipts;

public sealed class GetGoodsReceiptMetricsTests
{
    [Fact]
    public async Task Counts_ItemsAndDocuments_UsingTehranDayBoundaries()
    {
        await using var context = new ApplicationTestContext();
        var beforeToday = Receipt(new DateTimeOffset(2026, 10, 27, 20, 29, 0, TimeSpan.Zero));
        AddItem(beforeToday, null, null);
        AddItem(beforeToday, null, "R-unselected", statusUnselected: true);
        var today = Receipt(new DateTimeOffset(2026, 10, 27, 20, 30, 0, TimeSpan.Zero));
        AddItem(today, null, null);
        AddItem(today, new Invoice("INV-1", null), "R-1");
        var afterToday = Receipt(new DateTimeOffset(2026, 10, 28, 20, 30, 0, TimeSpan.Zero));
        AddItem(afterToday, null, null);
        var receipts = new[] { beforeToday, today, afterToday };
        var items = receipts.SelectMany(receipt => receipt.Items).ToArray();
        var receiptRepository = Repository(receipts);
        var itemRepository = Repository(items);
        context.UnitOfWork.Repository<GoodsReceipt>().Returns(receiptRepository);
        context.UnitOfWork.Repository<GoodsReceiptItem>().Returns(itemRepository);

        var result = await context.Sender.Send(new GetGoodsReceiptMetricsQuery(
            true, true, true, true,
            new DateTimeOffset(2026, 10, 27, 22, 0, 0, TimeSpan.Zero)));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.ItemsWithoutReceiptCount);
        Assert.Equal(1, result.Data.ItemsWithoutInvoiceCount);
        Assert.Equal(2, result.Data.TodayItemCount);
        Assert.Equal(1, result.Data.TodayDocumentCount);
    }

    [Fact]
    public async Task UnrequestedMetrics_AreNullAndNotQueried()
    {
        await using var context = new ApplicationTestContext();
        var items = Repository(Array.Empty<GoodsReceiptItem>());
        var receipts = Repository(Array.Empty<GoodsReceipt>());
        context.UnitOfWork.Repository<GoodsReceiptItem>().Returns(items);
        context.UnitOfWork.Repository<GoodsReceipt>().Returns(receipts);

        var result = await context.Sender.Send(new GetGoodsReceiptMetricsQuery(
            false, false, false, false, DateTimeOffset.UtcNow));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.ItemsWithoutReceiptCount);
        Assert.Null(result.Data.ItemsWithoutInvoiceCount);
        Assert.Null(result.Data.TodayItemCount);
        Assert.Null(result.Data.TodayDocumentCount);
        await items.DidNotReceive().CountAsync(Arg.Any<IQueryable<GoodsReceiptItem>>(), Arg.Any<CancellationToken>());
        await receipts.DidNotReceive().CountAsync(Arg.Any<IQueryable<GoodsReceipt>>(), Arg.Any<CancellationToken>());
    }

    private static GoodsReceipt Receipt(DateTimeOffset enteredAtUtc) =>
        new(enteredAtUtc, null, Guid.NewGuid(), Guid.NewGuid().ToString());

    private static void AddItem(GoodsReceipt receipt, Invoice? invoice, string? receiptNumber,
        bool statusUnselected = false)
    {
        receipt.AddItem(Guid.NewGuid(), Guid.NewGuid(),
            statusUnselected ? null : invoice is null ? InvoiceStatus.DoesNotHave : InvoiceStatus.HasInvoice,
            invoice, receiptNumber, 1, null, null, receipt.Items.Count);
        typeof(GoodsReceiptItem).GetProperty(nameof(GoodsReceiptItem.GoodsReceipt))!
            .SetValue(receipt.Items.Last(), receipt);
    }

    private static IRepositoryAsync<T, Guid> Repository<T>(T[] values)
        where T : Warehouse.Domain.Common.Entity<Guid>
    {
        var repository = Substitute.For<IRepositoryAsync<T, Guid>>();
        repository.Entities.Returns(values.AsQueryable());
        repository.CountAsync(Arg.Any<IQueryable<T>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<T>>().Count());
        return repository;
    }
}
