using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.GoodsIssues.Commands.DeleteGoodsIssue;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsIssues;

public sealed class DeleteGoodsIssueLinkTests
{
    [Fact]
    public async Task Delete_WithLinkedReceiptItems_IsRejected()
    {
        await using var context = new ApplicationTestContext();
        var issue = new GoodsIssue("OUT-1", DateTimeOffset.UtcNow, null, Guid.NewGuid());
        var issueRepository = Substitute.For<IRepositoryAsync<GoodsIssue, Guid>>();
        issueRepository.GetByIdAsync(issue.Id, Arg.Any<CancellationToken>()).Returns(issue);
        context.UnitOfWork.Repository<GoodsIssue>().Returns(issueRepository);
        var itemRepository = Substitute.For<IRepositoryAsync<GoodsIssueItem, Guid>>();
        itemRepository.Entities.Returns(Array.Empty<GoodsIssueItem>().AsQueryable());
        itemRepository.CountAsync(Arg.Any<IQueryable<GoodsIssueItem>>(), Arg.Any<CancellationToken>())
            .Returns(1);
        context.UnitOfWork.Repository<GoodsIssueItem>().Returns(itemRepository);

        var result = await context.Sender.Send(new DeleteGoodsIssueCommand(issue.Id));

        Assert.False(result.IsSuccess);
        Assert.Contains("ابتدا ارتباط آیتم‌های ورود کالا با این سند خروج را حذف کنید.", result.Messages);
        await issueRepository.DidNotReceive().DeleteAsync(Arg.Any<GoodsIssue>(), Arg.Any<CancellationToken>());
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }
}
