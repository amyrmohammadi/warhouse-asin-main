using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.GoodsIssues.Commands.DeleteGoodsIssue;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsIssues;

public sealed class DeleteGoodsIssueLinkTests
{
    [Fact]
    public async Task Delete_WithLinkedReceiptItems_DetachesTheLinkAndPreservesTheReceipt()
    {
        await using var context = new ApplicationTestContext();
        var issue = new GoodsIssue("OUT-1", DateTimeOffset.UtcNow, null, Guid.NewGuid());
        var issueRepository = Substitute.For<IRepositoryAsync<GoodsIssue, Guid>>();
        issueRepository.GetByIdAsync(issue.Id, Arg.Any<CancellationToken>()).Returns(issue);
        context.UnitOfWork.Repository<GoodsIssue>().Returns(issueRepository);
        var issueItem = issue.AddItem(null, "P", 1, null, null, null, null, null, null, 0);
        var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, null, Guid.NewGuid(), "1");
        receipt.AddItem(Guid.NewGuid(), Guid.NewGuid(), null, null, "REC-1", 5, null, null, 0);
        var receiptItem = receipt.Items.Single();
        receiptItem.SetGoodsIssueItem(issueItem);
        var itemRepository = Substitute.For<IRepositoryAsync<GoodsReceiptItem, Guid>>();
        itemRepository.Entities.Returns(new[] { receiptItem }.AsQueryable());
        itemRepository.ToListAsync(Arg.Any<IQueryable<GoodsReceiptItem>>(), Arg.Any<CancellationToken>())
            .Returns([receiptItem]);
        context.UnitOfWork.Repository<GoodsReceiptItem>().Returns(itemRepository);

        var result = await context.Sender.Send(new DeleteGoodsIssueCommand(issue.Id));

        Assert.True(result.IsSuccess);
        Assert.Null(receiptItem.GoodsIssueItemId);
        Assert.Null(receiptItem.GoodsIssueItem);
        Assert.Equal(5, receiptItem.Quantity);
        Assert.Equal("REC-1", receiptItem.ReceiptNumber);
        Assert.Single(receipt.Items);
        await issueRepository.Received(1).DeleteAsync(issue, Arg.Any<CancellationToken>());
        await context.UnitOfWork.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }
}
