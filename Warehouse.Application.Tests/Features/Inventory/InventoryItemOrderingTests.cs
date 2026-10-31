using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssueItems;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptItemRows;
using Warehouse.Application.Tests.TestSupport;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Tests.Features.Inventory;

public sealed class InventoryItemOrderingTests
{
    [Fact]
    public async Task ReceiptItems_OrderByPageDescendingThenEntryTimeAndFormRowOrder()
    {
        await using var context = new ApplicationTestContext();
        var person = new Person("تحویل‌دهنده", "");
        var unit = new UnitOfMeasure("عدد", null);
        var product = new Product("کالا", "P-1", unit.Id, null);
        var warehouse = new StorageWarehouse("انبار", null);
        Set(product, nameof(Product.UnitOfMeasure), unit);
        var page2 = new GoodsReceipt(DateTimeOffset.UtcNow.AddDays(-1), null, person.Id, "10");
        var page1 = new GoodsReceipt(DateTimeOffset.UtcNow, null, person.Id, "9");
        var laterPage10 = new GoodsReceipt(DateTimeOffset.UtcNow, null, person.Id, "10");
        // Receipt IDs must not determine the entry order within a page.
        Set(page2, nameof(GoodsReceipt.Id), Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        Set(laterPage10, nameof(GoodsReceipt.Id), Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Set(page2, nameof(GoodsReceipt.Deliverer), person);
        Set(page1, nameof(GoodsReceipt.Deliverer), person);
        Set(laterPage10, nameof(GoodsReceipt.Deliverer), person);
        page2.AddItem(product.Id, warehouse.Id, null, null, null, 1, null, "دوم", 1);
        page2.AddItem(product.Id, warehouse.Id, null, null, null, 1, null, "اول", 0);
        page1.AddItem(product.Id, warehouse.Id, null, null, null, 1, null, "صفحه اول", 0);
        laterPage10.AddItem(product.Id, warehouse.Id, null, null, null, 1, null, "ثبت بعدی", 0);
        var rows = laterPage10.Items.Concat(page1.Items).Concat(page2.Items).ToArray();
        foreach (var row in rows)
        {
            Set(row, nameof(GoodsReceiptItem.GoodsReceipt),
                row.GoodsReceiptId == page1.Id ? page1 : row.GoodsReceiptId == page2.Id ? page2 : laterPage10);
            Set(row, nameof(GoodsReceiptItem.Product), product);
            Set(row, nameof(GoodsReceiptItem.Warehouse), warehouse);
        }
        var repository = Substitute.For<IRepositoryAsync<GoodsReceiptItem, Guid>>();
        repository.Entities.Returns(rows.AsQueryable());
        repository.CountAsync(Arg.Any<IQueryable<GoodsReceiptItem>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<GoodsReceiptItem>>().Count());
        repository.ToListAsync(Arg.Any<IQueryable<GoodsReceiptItemRowSummary>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<GoodsReceiptItemRowSummary>>().ToList());
        context.UnitOfWork.Repository<GoodsReceiptItem>().Returns(repository);

        var result = await context.Sender.Send(new GetGoodsReceiptItemRowsQuery
        {
            PageNumber = 1, PageSize = 10
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(["اول", "دوم", "ثبت بعدی", "صفحه اول"],
            result.Data!.Items.Select(row => row.ItemDescription));

        var descending = await context.Sender.Send(new GetGoodsReceiptItemRowsQuery
        {
            PageNumber = 1, PageSize = 10, SortBy = "pageNumber", SortDescending = true
        });
        Assert.Equal(["اول", "دوم", "ثبت بعدی", "صفحه اول"],
            descending.Data!.Items.Select(row => row.ItemDescription));

        var ascending = await context.Sender.Send(new GetGoodsReceiptItemRowsQuery
        {
            PageNumber = 1, PageSize = 10, SortBy = "pageNumber", SortDescending = false
        });
        Assert.Equal(["صفحه اول", "اول", "دوم", "ثبت بعدی"],
            ascending.Data!.Items.Select(row => row.ItemDescription));

        var secondPage = await context.Sender.Send(new GetGoodsReceiptItemRowsQuery
        {
            PageNumber = 2, PageSize = 2
        });
        Assert.Equal(["ثبت بعدی", "صفحه اول"],
            secondPage.Data!.Items.Select(row => row.ItemDescription));
    }

    [Fact]
    public async Task IssueItems_OrderByIssueNumberThenFormRowOrder()
    {
        await using var context = new ApplicationTestContext();
        var person = new Person("خارج‌کننده", "");
        var destination = new GoodsIssueDestination("مقصد", null);
        var issue2 = new GoodsIssue("OUT-2", DateTimeOffset.UtcNow.AddDays(-1), null, person.Id);
        var issue1 = new GoodsIssue("OUT-1", DateTimeOffset.UtcNow, null, person.Id);
        Set(issue2, nameof(GoodsIssue.Issuer), person);
        Set(issue1, nameof(GoodsIssue.Issuer), person);
        issue2.AddItem(null, "SECOND", 1, null, destination.Id, "خروج", null, "واحد", null, 1);
        issue2.AddItem(null, "FIRST", 1, null, destination.Id, "خروج", null, "واحد", null, 0);
        issue1.AddItem(null, "PAGE-ONE", 1, null, destination.Id, "خروج", null, "واحد", null, 0);
        var rows = issue2.Items.Concat(issue1.Items).ToArray();
        foreach (var row in rows)
        {
            Set(row, nameof(GoodsIssueItem.GoodsIssue), row.GoodsIssueId == issue1.Id ? issue1 : issue2);
            Set(row, nameof(GoodsIssueItem.Destination), destination);
        }
        var repository = Substitute.For<IRepositoryAsync<GoodsIssueItem, Guid>>();
        repository.Entities.Returns(rows.AsQueryable());
        repository.CountAsync(Arg.Any<IQueryable<GoodsIssueItem>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<GoodsIssueItem>>().Count());
        repository.ToListAsync(Arg.Any<IQueryable<GoodsIssueItemRowSummary>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<GoodsIssueItemRowSummary>>().ToList());
        context.UnitOfWork.Repository<GoodsIssueItem>().Returns(repository);

        var result = await context.Sender.Send(new GetGoodsIssueItemsQuery
        {
            PageNumber = 1, PageSize = 10
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(["PAGE-ONE", "FIRST", "SECOND"],
            result.Data!.Items.Select(row => row.ProductCode));

        var descending = await context.Sender.Send(new GetGoodsIssueItemsQuery
        {
            PageNumber = 1, PageSize = 10, SortBy = "issueNumber", SortDescending = true
        });
        Assert.Equal(["FIRST", "SECOND", "PAGE-ONE"],
            descending.Data!.Items.Select(row => row.ProductCode));
    }

    private static void Set<T>(T entity, string property, object value) where T : class =>
        typeof(T).GetProperty(property)!.SetValue(entity, value);
}
