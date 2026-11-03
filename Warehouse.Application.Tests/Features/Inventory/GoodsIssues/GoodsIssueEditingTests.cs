using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsIssues.Commands.SaveGoodsIssue;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsIssues;

public sealed class GoodsIssueEditingTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task ChangingProduct_RequiresDetachingTheLinkedReceiptFirst(bool linked, bool codeOnly)
    {
        await using var context = new ApplicationTestContext();
        var person = new Person("خارج‌کننده", "");
        var unit = new UnitOfMeasure("عدد", null);
        var oldProduct = new Product("قبلی", "OLD", unit.Id, null);
        var newProduct = new Product("جدید", "NEW", unit.Id, null);
        var destination = new GoodsIssueDestination("مقصد", null);
        var issue = new GoodsIssue("OUT-1", DateTimeOffset.UtcNow, null, person.Id);
        var item = issue.AddItem(oldProduct.Id, oldProduct.Code, 1, null, destination.Id, null, null, null, null, 0);
        var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, null, person.Id, "1");
        receipt.AddItem(oldProduct.Id, Guid.NewGuid(), null, null, "REC-1", 1, null, null, 0);
        var receiptItem = receipt.Items.Single();
        if (linked) receiptItem.SetGoodsIssueItem(item);
        var issues = Register(context, new List<GoodsIssue> { issue });
        issues.ToListAsync(Arg.Any<IQueryable<GoodsIssueSummary>>(), Arg.Any<CancellationToken>())
            .Returns([new GoodsIssueSummary { Id = issue.Id }]);
        Register(context, issue.Items.ToList());
        Register(context, new List<GoodsReceiptItem> { receiptItem });
        Register(context, new List<Person> { person });
        Register(context, new List<Product> { oldProduct, newProduct });
        Register(context, new List<UnitOfMeasure> { unit });
        Register(context, new List<GoodsIssueDestination> { destination });
        Register(context, new List<Supplier>());
        var result = await context.Sender.Send(new SaveGoodsIssueCommand(issue.Id, "OUT-CHANGED",
            issue.IssuedAtUtc, "تغییر", person.Id, null,
            [new SaveGoodsIssueItem(codeOnly ? oldProduct.Id : newProduct.Id, newProduct.Code, 1, null,
                destination.Id, null, GoodsIssueExitReason.Other, null, null, null, Id: item.Id)], issue.ConcurrencyStamp));
        Assert.Equal(!linked, result.IsSuccess);
        if (linked)
        {
            Assert.Contains(result.Messages, message => message.Contains("ابتدا ارتباط شماره خروج") && message.Contains("هر دو سند"));
            Assert.Equal(oldProduct.Id, item.ProductId);
            Assert.Equal(oldProduct.Code, item.ProductCode);
            Assert.Equal("OUT-1", issue.IssueNumber);
            Assert.Equal(item.Id, receiptItem.GoodsIssueItemId);
            await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
        }
        else Assert.Equal(newProduct.Id, item.ProductId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SaveLinkedIssue_PreservesRetainedItemIds_AndOnlyDetachesRemovedItems(bool removeLinked, bool finalized)
    {
        await using var context = new ApplicationTestContext();
        var person = new Person("خارج‌کننده", "");
        var unit = new UnitOfMeasure("عدد", null);
        var product = new Product("کالا", "P-1", unit.Id, null);
        var destination = new GoodsIssueDestination("مقصد", null);
        var issue = new GoodsIssue("OUT-1", DateTimeOffset.UtcNow, null, person.Id);
        var linkedItem = issue.AddItem(product.Id, product.Code, 1, null, destination.Id, null, null, null, null, 0);
        var otherItem = issue.AddItem(product.Id, product.Code, 2, null, destination.Id, null, null, null, null, 1);
        issue.SetFinalized(finalized);
        var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, null, person.Id, "1");
        receipt.AddItem(product.Id, Guid.NewGuid(), null, null, "REC-1", 5, null, null, 0);
        var receiptItem = receipt.Items.Single();
        receiptItem.SetGoodsIssueItem(linkedItem);
        var issues = Register(context, new List<GoodsIssue> { issue });
        issues.ToListAsync(Arg.Any<IQueryable<GoodsIssueSummary>>(), Arg.Any<CancellationToken>())
            .Returns([new GoodsIssueSummary { Id = issue.Id }]);
        var items = Register(context, issue.Items.ToList());
        Register(context, new List<GoodsReceiptItem> { receiptItem });
        Register(context, new List<Person> { person });
        Register(context, new List<Product> { product });
        Register(context, new List<UnitOfMeasure> { unit });
        Register(context, new List<GoodsIssueDestination> { destination });
        Register(context, new List<Supplier>());
        var selected = removeLinked ? new[] { otherItem } : new[] { linkedItem, otherItem };
        var requests = selected.Select(item => new SaveGoodsIssueItem(product.Id, product.Code, 9, null,
            destination.Id, null, GoodsIssueExitReason.Other, "ویرایش", null, null, Id: item.Id)).ToList();
        requests.Add(new SaveGoodsIssueItem(product.Id, product.Code, 3, null, destination.Id, null,
            GoodsIssueExitReason.Other, "جدید", null, null));

        var result = await context.Sender.Send(new SaveGoodsIssueCommand(issue.Id, "OUT-EDITED",
            issue.IssuedAtUtc, "ویرایش سند", person.Id, null, requests, issue.ConcurrencyStamp));

        Assert.True(result.IsSuccess, string.Join("; ", result.Messages));
        Assert.Equal("OUT-EDITED", issue.IssueNumber);
        Assert.Equal(finalized, issue.IsFinalized);
        Assert.Equal(selected.Length + 1, issue.Items.Count);
        foreach (var retained in selected)
        {
            Assert.Contains(issue.Items, item => item.Id == retained.Id && item.Quantity == 9);
            await items.DidNotReceive().DeleteAsync(retained, Arg.Any<CancellationToken>());
        }
        Assert.Equal(removeLinked ? null : linkedItem.Id, receiptItem.GoodsIssueItemId);
        Assert.Equal(5, receiptItem.Quantity);
        Assert.Equal("REC-1", receiptItem.ReceiptNumber);
        Assert.Single(receipt.Items);
        await context.UnitOfWork.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    private static IRepositoryAsync<T, Guid> Register<T>(ApplicationTestContext context, List<T> values) where T : Entity<Guid>
    {
        var repository = Substitute.For<IRepositoryAsync<T, Guid>>();
        repository.Entities.Returns(_ => values.AsQueryable());
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => values.FirstOrDefault(value => value.Id == call.Arg<Guid>()));
        repository.CountAsync(Arg.Any<IQueryable<T>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<T>>().Count());
        repository.ToListAsync(Arg.Any<IQueryable<T>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<T>>().ToList());
        repository.AddAsync(Arg.Any<T>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var item = call.Arg<T>(); values.Add(item); return item;
        });
        repository.When(value => value.DeleteAsync(Arg.Any<T>(), Arg.Any<CancellationToken>()))
            .Do(call => values.Remove(call.Arg<T>()));
        context.UnitOfWork.Repository<T>().Returns(repository);
        return repository;
    }
}
