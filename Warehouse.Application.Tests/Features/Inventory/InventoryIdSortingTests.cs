using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssueItems;
using Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssues;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptItemRows;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceipts;
using Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoices;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory;

public sealed class InventoryIdSortingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RowNumberSort_UsesDatabaseIdBeforePagination_InAllMainTables(bool descending)
    {
        await using var context = new ApplicationTestContext();
        var person = new Person("person", "");
        var unit = new UnitOfMeasure("unit", null);
        var product = new Product("product", "P", unit.Id, null);
        Set(product, nameof(Product.UnitOfMeasure), unit);
        var warehouse = new StorageWarehouse("warehouse", null);
        var receipts = Enumerable.Range(1, 4).Select(index =>
        {
            var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, null, person.Id, index.ToString());
            Set(receipt, nameof(GoodsReceipt.Deliverer), person);
            receipt.AddItem(product.Id, warehouse.Id, InvoiceStatus.DoesNotHave, null, null, 1, null, null, 0);
            var item = receipt.Items.Single();
            Set(item, nameof(GoodsReceiptItem.GoodsReceipt), receipt);
            Set(item, nameof(GoodsReceiptItem.Product), product);
            Set(item, nameof(GoodsReceiptItem.Warehouse), warehouse);
            return receipt;
        }).ToArray();
        var issues = Enumerable.Range(1, 4).Select(index =>
        {
            var issue = new GoodsIssue(index.ToString(), DateTimeOffset.UtcNow, null, person.Id);
            Set(issue, nameof(GoodsIssue.Issuer), person);
            var item = issue.AddItem(null, "P", 1, null, null, null, null, null, null, 0);
            Set(item, nameof(GoodsIssueItem.GoodsIssue), issue);
            return issue;
        }).ToArray();
        var invoices = Enumerable.Range(1, 4).Select(index => new Invoice(index.ToString(), null)).ToArray();
        var receiptItems = receipts.SelectMany(receipt => receipt.Items).ToArray();
        var issueItems = issues.SelectMany(issue => issue.Items).ToArray();
        Register<GoodsReceipt, GoodsReceiptSummary>(context, receipts);
        Register<GoodsReceiptItem, GoodsReceiptItemRowSummary>(context, receiptItems);
        Register<GoodsIssue, GoodsIssueSummary>(context, issues);
        Register<GoodsIssueItem, GoodsIssueItemRowSummary>(context, issueItems);
        Register<Invoice, InvoiceSummary>(context, invoices);

        var receiptResult = await context.Sender.Send(new GetGoodsReceiptsQuery
            { SortBy = "id", SortDescending = descending, PageNumber = 2, PageSize = 2 });
        var receiptItemResult = await context.Sender.Send(new GetGoodsReceiptItemRowsQuery
            { SortBy = "id", SortDescending = descending, PageNumber = 2, PageSize = 2 });
        var issueResult = await context.Sender.Send(new GetGoodsIssuesQuery
            { SortBy = "id", SortDescending = descending, PageNumber = 2, PageSize = 2 });
        var issueItemResult = await context.Sender.Send(new GetGoodsIssueItemsQuery
            { SortBy = "id", SortDescending = descending, PageNumber = 2, PageSize = 2 });
        var invoiceResult = await context.Sender.Send(new GetInvoicesQuery
            { SortBy = "id", SortDescending = descending, PageNumber = 2, PageSize = 2 });

        Assert.Equal(Expected(receipts, descending), receiptResult.Data!.Items.Select(item => item.Id));
        Assert.Equal(Expected(receiptItems, descending), receiptItemResult.Data!.Items.Select(item => item.Id));
        Assert.Equal(Expected(issues, descending), issueResult.Data!.Items.Select(item => item.Id));
        Assert.Equal(Expected(issueItems, descending), issueItemResult.Data!.Items.Select(item => item.Id));
        Assert.Equal(Expected(invoices, descending), invoiceResult.Data!.Items.Select(item => item.Id));
    }

    private static IEnumerable<Guid> Expected<T>(IEnumerable<T> values, bool descending) where T : Entity<Guid> =>
        (descending ? values.OrderByDescending(value => value.Id) : values.OrderBy(value => value.Id))
        .Skip(2).Take(2).Select(value => value.Id);

    private static void Set(object target, string property, object value) =>
        target.GetType().GetProperty(property)!.SetValue(target, value);

    private static void Register<T, TSummary>(ApplicationTestContext context, T[] values) where T : Entity<Guid>
    {
        var repository = Substitute.For<IRepositoryAsync<T, Guid>>();
        repository.Entities.Returns(values.AsQueryable());
        repository.CountAsync(Arg.Any<IQueryable<T>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<T>>().Count());
        repository.ToListAsync(Arg.Any<IQueryable<TSummary>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<TSummary>>().ToList());
        context.UnitOfWork.Repository<T>().Returns(repository);
    }
}
