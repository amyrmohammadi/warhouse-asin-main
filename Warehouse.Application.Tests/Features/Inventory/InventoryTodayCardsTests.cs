using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssueItems;
using Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssueMetrics;
using Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssues;
using Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoiceMetrics;
using Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoices;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory;

public sealed class InventoryTodayCardsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 20, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(InventoryDateQuickFilter.Today, 2, 4)]
    [InlineData(InventoryDateQuickFilter.CreatedToday, 2, 2)]
    public async Task GoodsIssueCards_MatchFilteredRows_InBothModes_AtTehranDayBoundaries(
        InventoryDateQuickFilter filter, int expectedDocuments, int expectedItems)
    {
        await using var context = new ApplicationTestContext(new Clock());
        var dates = new[] { Start.AddTicks(-1), Start, Start.AddDays(1).AddTicks(-1), Start.AddDays(1) };
        var issues = dates.Select((date, index) =>
        {
            var issue = new GoodsIssue($"OUT-{index}", date, null, Guid.NewGuid());
            SetCreated(issue, index == 0 ? Start : index == 1 ? Start.AddTicks(-1) : date);
            for (var row = 0; row < 2; row++)
            {
                var item = issue.AddItem(null, "P", 1, null, null, null, null, null, null, row);
                typeof(GoodsIssueItem).GetProperty(nameof(GoodsIssueItem.GoodsIssue))!.SetValue(item, issue);
                if (row == 0) SetCreated(item, date);
            }
            return issue;
        }).ToArray();
        var documents = Register(context, issues);
        documents.ToListAsync(Arg.Any<IQueryable<GoodsIssueSummary>>(), Arg.Any<CancellationToken>()).Returns([]);
        var items = Register(context, issues.SelectMany(issue => issue.Items).ToArray());
        items.ToListAsync(Arg.Any<IQueryable<GoodsIssueItemRowSummary>>(), Arg.Any<CancellationToken>()).Returns([]);

        var metrics = (await context.Sender.Send(new GetGoodsIssueMetricsQuery())).Data!;
        Assert.Equal(2, metrics.TodayDocumentCount);
        Assert.Equal(4, metrics.TodayItemCount);
        Assert.Equal(2, metrics.TodayCreatedDocumentCount);
        Assert.Equal(2, metrics.TodayCreatedItemCount);
        var documentRows = await context.Sender.Send(new GetGoodsIssuesQuery { QuickFilter = filter, PageSize = 1 });
        var itemRows = await context.Sender.Send(new GetGoodsIssueItemsQuery { QuickFilter = filter, PageSize = 1 });
        Assert.Equal(expectedDocuments, documentRows.Data!.FilteredCount);
        Assert.Equal(expectedItems, itemRows.Data!.FilteredCount);
        Assert.Equal(4, documentRows.Data.TotalCount);
        Assert.Equal(8, itemRows.Data.TotalCount);
    }

    [Theory]
    [InlineData(InventoryDateQuickFilter.Today, 2)]
    [InlineData(InventoryDateQuickFilter.ItemsToday, 1)]
    [InlineData(InventoryDateQuickFilter.CreatedToday, 1)]
    [InlineData(InventoryDateQuickFilter.ItemsCreatedToday, 1)]
    public async Task InvoiceCards_DistinguishInvoiceDateFromCreation_AndIncludeStandaloneInvoices(
        InventoryDateQuickFilter filter, int expected)
    {
        await using var context = new ApplicationTestContext(new Clock());
        var today = new DateOnly(2026, 11, 4);
        var first = new Invoice("FIRST", null);
        first.UpdateDetails(null, today, InvoiceProcessingStatus.UnderReview);
        SetCreated(first, Start.AddTicks(-1));
        var second = new Invoice("SECOND", null);
        second.UpdateDetails(null, today.AddDays(-1), InvoiceProcessingStatus.UnderReview);
        SetCreated(second, Start);
        var standalone = new Invoice("STANDALONE", null);
        standalone.UpdateDetails(null, today, InvoiceProcessingStatus.UnderReview);
        SetCreated(standalone, Start.AddDays(1));
        var undated = new Invoice("UNDATED", null);
        var receipt = new GoodsReceipt(Start, null, Guid.NewGuid(), "1");
        var pairs = new[] { (first, Start), (first, Start.AddTicks(-1)),
            (second, Start.AddTicks(-1)), (undated, Start.AddDays(1)) };
        foreach (var (invoice, created) in pairs)
        {
            receipt.AddItem(Guid.NewGuid(), Guid.NewGuid(), InvoiceStatus.HasInvoice, invoice, null, 1, null, null, receipt.Items.Count);
            var item = receipt.Items.Last();
            SetCreated(item, created);
            invoice.GoodsReceiptItems.Add(item);
        }
        var invoices = Register(context, new[] { first, second, standalone, undated });
        invoices.ToListAsync(Arg.Any<IQueryable<InvoiceSummary>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<InvoiceSummary>>().ToList());
        Register(context, receipt.Items.ToArray());

        var metrics = (await context.Sender.Send(new GetInvoiceMetricsQuery())).Data!;
        Assert.Equal(today, metrics.Today);
        Assert.Equal(2, metrics.TodayInvoiceCount);
        Assert.Equal(2, metrics.TodayItemCount);
        Assert.Equal(1, metrics.TodayCreatedInvoiceCount);
        Assert.Equal(1, metrics.TodayCreatedItemCount);
        var result = await context.Sender.Send(new GetInvoicesQuery { QuickFilter = filter, PageSize = 1 });
        Assert.Equal(4, result.Data!.TotalCount);
        Assert.Equal(expected, result.Data.FilteredCount);
    }

    private static void SetCreated(AuditableEntity entity, DateTimeOffset date) =>
        typeof(AuditableEntity).GetProperty(nameof(AuditableEntity.CreatedOn))!.SetValue(entity, date);

    private static IRepositoryAsync<T, Guid> Register<T>(ApplicationTestContext context, T[] values) where T : Entity<Guid>
    {
        var repository = Substitute.For<IRepositoryAsync<T, Guid>>();
        repository.Entities.Returns(values.AsQueryable());
        repository.CountAsync(Arg.Any<IQueryable<T>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<T>>().Count());
        context.UnitOfWork.Repository<T>().Returns(repository);
        return repository;
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Start.AddMinutes(30);
    }
}
