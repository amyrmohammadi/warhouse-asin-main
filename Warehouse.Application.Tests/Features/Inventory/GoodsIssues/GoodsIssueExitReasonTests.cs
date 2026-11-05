using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssueItems;
using Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssues;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsIssues;

public sealed class GoodsIssueExitReasonTests
{
    [Theory]
    [InlineData("تعمیرات", GoodsIssueExitReason.Repair)]
    [InlineData("Maintenance", GoodsIssueExitReason.Repair)]
    [InlineData("تعمیر", GoodsIssueExitReason.Repair)]
    [InlineData("شارژ", GoodsIssueExitReason.Charge)]
    [InlineData("عدم تایید", GoodsIssueExitReason.NotApproved)]
    [InlineData("عدم تأیید", GoodsIssueExitReason.NotApproved)]
    public void Import_RecognizesReasons(string text, GoodsIssueExitReason expected)
    {
        Assert.True(GoodsIssueExitReasonParser.TryFromText(text, out var reason));
        Assert.Equal(expected, reason);
    }

    [Fact]
    public void OldRepairText_IsReplaced_AndDoesNotOverrideEdits()
    {
        var issue = new GoodsIssue("OUT-1", DateTimeOffset.UtcNow, null, Guid.NewGuid());
        var item = issue.AddItem(null, "P", 1, null, null, "تعمیرات", null, null, null, 0);
        Assert.Equal(GoodsIssueExitReason.Repair, item.ExitReasonType);
        Assert.Equal("تعمیر", item.ExitReason);
        item.Update(null, "P", 1, null, null, null, GoodsIssueExitReason.Charge, true, null, null, null, 0);
        Assert.Equal(GoodsIssueExitReason.Charge, item.ExitReasonType);
        Assert.Null(item.ExitReason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultipleReasons_FilterItemsAndDocuments_BeforePagination(bool filter)
    {
        await using var context = new ApplicationTestContext();
        var person = new Person("خارج‌کننده", "");
        var first = new GoodsIssue("OUT-1", DateTimeOffset.UtcNow, null, person.Id);
        var second = new GoodsIssue("OUT-2", DateTimeOffset.UtcNow, null, person.Id);
        var third = new GoodsIssue("OUT-3", DateTimeOffset.UtcNow, null, person.Id);
        first.AddItem(null, "REPAIR", 1, null, null, "تعمیر", null, null, null, 0);
        first.AddItem(null, "SALE", 1, null, null, "فروش", null, null, null, 1);
        second.AddItem(null, "CHARGE", 1, null, null, "شارژ", null, null, null, 0);
        third.AddItem(null, "REJECTED", 1, null, null, "عدم تایید", null, null, null, 0);
        GoodsIssue[] documents = [first, second, third];
        foreach (var document in documents)
        {
            typeof(GoodsIssue).GetProperty(nameof(GoodsIssue.Issuer))!.SetValue(document, person);
            foreach (var item in document.Items)
                typeof(GoodsIssueItem).GetProperty(nameof(GoodsIssueItem.GoodsIssue))!.SetValue(item, document);
        }
        var issues = Register(context, documents);
        issues.ToListAsync(Arg.Any<IQueryable<GoodsIssueSummary>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<GoodsIssueSummary>>().ToList());
        var items = Register(context, documents.SelectMany(issue => issue.Items).ToArray());
        items.ToListAsync(Arg.Any<IQueryable<GoodsIssueItemRowSummary>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<GoodsIssueItemRowSummary>>().ToList());
        GoodsIssueExitReason[] reasons = filter ? [GoodsIssueExitReason.Repair, GoodsIssueExitReason.Charge] : [];
        var itemResult = await context.Sender.Send(new GetGoodsIssueItemsQuery { ExitReasons = reasons, PageSize = 1, PageNumber = 2 });
        var issueResult = await context.Sender.Send(new GetGoodsIssuesQuery { ExitReasons = reasons, PageSize = 1, PageNumber = 2 });
        Assert.True(itemResult.IsSuccess);
        Assert.True(issueResult.IsSuccess);
        Assert.Equal(4, itemResult.Data!.TotalCount);
        Assert.Equal(3, issueResult.Data!.TotalCount);
        Assert.Equal(filter ? 2 : 4, itemResult.Data.FilteredCount);
        Assert.Equal(filter ? 2 : 3, issueResult.Data.FilteredCount);
        Assert.Single(itemResult.Data.Items);
        Assert.Single(issueResult.Data.Items);
        if (filter)
        {
            Assert.Equal("CHARGE", itemResult.Data.Items.Single().ProductCode);
            Assert.Equal("OUT-2", issueResult.Data.Items.Single().IssueNumber);
        }
    }

    private static IRepositoryAsync<T, Guid> Register<T>(ApplicationTestContext context, T[] values) where T : Entity<Guid>
    {
        var repository = Substitute.For<IRepositoryAsync<T, Guid>>();
        repository.Entities.Returns(values.AsQueryable());
        repository.CountAsync(Arg.Any<IQueryable<T>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<T>>().Count());
        context.UnitOfWork.Repository<T>().Returns(repository);
        return repository;
    }
}
