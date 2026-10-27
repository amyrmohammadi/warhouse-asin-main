using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptItemRows;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceipts;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsReceipts;

public sealed class GetGoodsReceiptQuickFilterTests
{
    [Fact]
    public async Task ItemFilters_SelectExactlyTheMatchingItems()
    {
        await using var context = new ApplicationTestContext();
        var oldReceipt = Receipt(DateTimeOffset.UtcNow.AddDays(-2));
        AddItem(oldReceipt, null, null);
        var todayReceipt = Receipt(DateTimeOffset.UtcNow);
        AddItem(todayReceipt, new Invoice("INV-1", null), "R-1");
        AddItem(todayReceipt, null, "R-2");
        AddItem(todayReceipt, null, "R-3", statusUnselected: true);
        var repository = ItemRepository(oldReceipt.Items.Concat(todayReceipt.Items).ToArray());
        context.UnitOfWork.Repository<GoodsReceiptItem>().Returns(repository);

        var missingReceipt = await context.Sender.Send(new GetGoodsReceiptItemRowsQuery
            { QuickFilter = GoodsReceiptQuickFilter.MissingReceipt });
        var missingInvoice = await context.Sender.Send(new GetGoodsReceiptItemRowsQuery
            { QuickFilter = GoodsReceiptQuickFilter.MissingInvoice });
        var emptyReceipt = await context.Sender.Send(new GetGoodsReceiptItemRowsQuery
            { ReceiptNumberIsEmpty = true });
        var notEntered = await context.Sender.Send(new GetGoodsReceiptItemRowsQuery
            { InvoiceStatus = InvoiceStatusFilter.NotEntered });
        var today = await context.Sender.Send(new GetGoodsReceiptItemRowsQuery
            { QuickFilter = GoodsReceiptQuickFilter.Today });

        Assert.Equal(4, missingReceipt.Data!.TotalCount);
        Assert.Equal(1, missingReceipt.Data.FilteredCount);
        Assert.Equal(1, missingInvoice.Data!.FilteredCount);
        Assert.Equal(1, emptyReceipt.Data!.FilteredCount);
        Assert.Equal(1, notEntered.Data!.FilteredCount);
        Assert.Equal(3, today.Data!.FilteredCount);
    }

    [Fact]
    public async Task DocumentTodayFilter_SelectsDocumentsNotItemCount()
    {
        await using var context = new ApplicationTestContext();
        var oldReceipt = Receipt(DateTimeOffset.UtcNow.AddDays(-2));
        AddItem(oldReceipt, null, null);
        var todayReceipt = Receipt(DateTimeOffset.UtcNow);
        AddItem(todayReceipt, null, null);
        AddItem(todayReceipt, null, "R-2");
        var repository = ReceiptRepository([oldReceipt, todayReceipt]);
        context.UnitOfWork.Repository<GoodsReceipt>().Returns(repository);

        var result = await context.Sender.Send(new GetGoodsReceiptsQuery
            { QuickFilter = GoodsReceiptQuickFilter.Today });

        Assert.Equal(2, result.Data!.TotalCount);
        Assert.Equal(1, result.Data.FilteredCount);
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

    private static IRepositoryAsync<GoodsReceiptItem, Guid> ItemRepository(GoodsReceiptItem[] values)
    {
        var repository = Substitute.For<IRepositoryAsync<GoodsReceiptItem, Guid>>();
        repository.Entities.Returns(values.AsQueryable());
        repository.CountAsync(Arg.Any<IQueryable<GoodsReceiptItem>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<GoodsReceiptItem>>().Count());
        repository.ToListAsync(Arg.Any<IQueryable<GoodsReceiptItemRowSummary>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<GoodsReceiptItemRowSummary>());
        return repository;
    }

    private static IRepositoryAsync<GoodsReceipt, Guid> ReceiptRepository(GoodsReceipt[] values)
    {
        var repository = Substitute.For<IRepositoryAsync<GoodsReceipt, Guid>>();
        repository.Entities.Returns(values.AsQueryable());
        repository.CountAsync(Arg.Any<IQueryable<GoodsReceipt>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<GoodsReceipt>>().Count());
        repository.ToListAsync(Arg.Any<IQueryable<GoodsReceiptSummary>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<GoodsReceiptSummary>());
        return repository;
    }
}
