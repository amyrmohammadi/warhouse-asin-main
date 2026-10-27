using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.LinkGoodsIssueItem;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsReceipts;

public sealed class LinkGoodsIssueItemTests
{
    [Fact]
    public async Task Link_MultipleReceiptItemsToOneIssueItem_AndClearOne()
    {
        await using var context = new ApplicationTestContext();
        var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, null, Guid.NewGuid(), "1");
        receipt.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, null, null, 1, null, null, 0);
        receipt.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, null, null, 1, null, null, 1);
        var items = receipt.Items.ToArray();
        var issue = new GoodsIssue("OUT-1", DateTimeOffset.UtcNow, null, Guid.NewGuid());
        var issueItem = issue.AddItem(null, "P-1", 2, null, Guid.NewGuid(), "خروج",
            null, "واحد", null, 0);
        var receiptItemRepository = Substitute.For<IRepositoryAsync<GoodsReceiptItem, Guid>>();
        receiptItemRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => items.SingleOrDefault(item => item.Id == call.Arg<Guid>()));
        var issueItemRepository = Substitute.For<IRepositoryAsync<GoodsIssueItem, Guid>>();
        issueItemRepository.GetByIdAsync(issueItem.Id, Arg.Any<CancellationToken>()).Returns(issueItem);
        var receiptRepository = Substitute.For<IRepositoryAsync<GoodsReceipt, Guid>>();
        receiptRepository.GetByIdAsync(receipt.Id, Arg.Any<CancellationToken>()).Returns(receipt);
        context.UnitOfWork.Repository<GoodsReceiptItem>().Returns(receiptItemRepository);
        context.UnitOfWork.Repository<GoodsIssueItem>().Returns(issueItemRepository);
        context.UnitOfWork.Repository<GoodsReceipt>().Returns(receiptRepository);

        foreach (var item in items)
        {
            var result = await context.Sender.Send(new LinkGoodsIssueItemCommand(
                item.Id, issueItem.Id, receipt.ConcurrencyStamp));
            Assert.True(result.IsSuccess);
            Assert.Equal(issueItem.Id, result.Data!.GoodsIssueItemId);
            Assert.Equal(receipt.ConcurrencyStamp, result.Data.ConcurrencyStamp);
        }

        Assert.Equal(2, issueItem.GoodsReceiptItems.Count);
        Assert.All(items, item => Assert.Equal(issueItem.Id, item.GoodsIssueItemId));

        var cleared = await context.Sender.Send(new LinkGoodsIssueItemCommand(
            items[0].Id, null, receipt.ConcurrencyStamp));
        Assert.True(cleared.IsSuccess);
        Assert.Null(items[0].GoodsIssueItemId);
        Assert.Null(items[0].GoodsIssueItem);
        Assert.Equal(issueItem.Id, items[1].GoodsIssueItemId);
        Assert.Single(issueItem.GoodsReceiptItems);
        await context.UnitOfWork.Received(3).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Link_WithStaleReceiptStamp_DoesNotChangeRelationship()
    {
        await using var context = new ApplicationTestContext();
        var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, null, Guid.NewGuid(), "1");
        receipt.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, null, null, 1, null, null, 0);
        var item = receipt.Items.Single();
        var issue = new GoodsIssue("OUT-1", DateTimeOffset.UtcNow, null, Guid.NewGuid());
        var issueItem = issue.AddItem(null, "P-1", 1, null, Guid.NewGuid(), "خروج",
            null, "واحد", null, 0);
        context.UnitOfWork.Repository<GoodsReceiptItem>().GetByIdAsync(item.Id, Arg.Any<CancellationToken>())
            .Returns(item);
        context.UnitOfWork.Repository<GoodsIssueItem>().GetByIdAsync(issueItem.Id, Arg.Any<CancellationToken>())
            .Returns(issueItem);
        context.UnitOfWork.Repository<GoodsReceipt>().GetByIdAsync(receipt.Id, Arg.Any<CancellationToken>())
            .Returns(receipt);
        var staleStamp = Guid.NewGuid();
        while (staleStamp == receipt.ConcurrencyStamp) staleStamp = Guid.NewGuid();

        var result = await context.Sender.Send(new LinkGoodsIssueItemCommand(
            item.Id, issueItem.Id, staleStamp));

        Assert.False(result.IsSuccess);
        Assert.Contains(ConcurrencyMessages.Conflict, result.Messages);
        Assert.Null(item.GoodsIssueItemId);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }
}
