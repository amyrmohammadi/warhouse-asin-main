using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.GoodsIssues.Commands.DeleteGoodsIssue;
using Warehouse.Application.Features.Inventory.GoodsIssues.Commands.SaveGoodsIssue;
using Warehouse.Application.Features.Inventory.GoodsIssues.Commands.SetGoodsIssueFinalization;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsIssues;

public sealed class GoodsIssueFinalizationTests
{
    [Fact]
    public async Task FinalizeAndReopen_AdvanceStamp_AndEnableEditingAgain()
    {
        await using var context = new ApplicationTestContext();
        var issue = Issue("2026-10-20T12:00:00Z");
        Register(context, issue);
        var original = issue.ConcurrencyStamp;
        var result = await context.Sender.Send(new SetGoodsIssueFinalizationCommand(true, issue.Id,
            ConcurrencyStamp: original));
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data);
        Assert.True(issue.IsFinalized);
        Assert.NotEqual(original, issue.ConcurrencyStamp);
        Assert.Throws<InvalidOperationException>(() => issue.Update("OUT-2", issue.IssuedAtUtc, null, issue.IssuerId));
        Assert.Throws<InvalidOperationException>(() => issue.AddItem(null, "P", 1, null, null, null,
            GoodsIssueExitReason.Other, false, null, null, null, 0));

        var reopened = await context.Sender.Send(new SetGoodsIssueFinalizationCommand(false, issue.Id,
            ConcurrencyStamp: issue.ConcurrencyStamp));
        Assert.True(reopened.IsSuccess);
        Assert.False(issue.IsFinalized);
        issue.Update("OUT-2", issue.IssuedAtUtc, null, issue.IssuerId);
        Assert.Equal("OUT-2", issue.IssueNumber);
    }

    [Fact]
    public async Task DailyFinalization_UsesEntireTehranDay_AndSkipsAlreadyFinalizedDocuments()
    {
        await using var context = new ApplicationTestContext();
        var before = Issue("2026-10-20T20:29:59Z");
        var first = Issue("2026-10-20T20:30:00Z");
        var last = Issue("2026-10-21T20:29:59Z");
        var after = Issue("2026-10-21T20:30:00Z");
        var already = Issue("2026-10-21T12:00:00Z");
        already.SetFinalized(true);
        Register(context, before, first, last, after, already);

        var result = await context.Sender.Send(new SetGoodsIssueFinalizationCommand(true,
            Day: new DateOnly(2026, 10, 21)));
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data);
        Assert.False(before.IsFinalized);
        Assert.True(first.IsFinalized);
        Assert.True(last.IsFinalized);
        Assert.False(after.IsFinalized);
        Assert.True(already.IsFinalized);
        await context.UnitOfWork.Received(1).BeginSerializableTransaction(Arg.Any<CancellationToken>());
        await context.UnitOfWork.Received(1).CommitTransaction(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finalize_WithStaleStamp_DoesNotSave()
    {
        await using var context = new ApplicationTestContext();
        var issue = Issue("2026-10-21T12:00:00Z");
        Register(context, issue);
        var result = await context.Sender.Send(new SetGoodsIssueFinalizationCommand(true, issue.Id,
            ConcurrencyStamp: Guid.NewGuid()));
        Assert.False(result.IsSuccess);
        Assert.Contains(ConcurrencyMessages.Conflict, result.Messages);
        Assert.False(issue.IsFinalized);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalizedDocument_CannotBeSavedOrDeleted()
    {
        await using var context = new ApplicationTestContext();
        var issue = Issue("2026-10-21T12:00:00Z");
        issue.SetFinalized(true);
        Register(context, issue);
        var save = await context.Sender.Send(new SaveGoodsIssueCommand(issue.Id, issue.IssueNumber,
            issue.IssuedAtUtc, null, issue.IssuerId, null,
            [new SaveGoodsIssueItem(Guid.NewGuid(), "P-1", 1, null, Guid.NewGuid(), null,
                GoodsIssueExitReason.Other, null, null, null)], issue.ConcurrencyStamp));
        Assert.False(save.IsSuccess);
        Assert.Contains(GoodsIssue.FinalizedMessage, save.Messages);
        var delete = await context.Sender.Send(new DeleteGoodsIssueCommand(issue.Id));
        Assert.False(delete.IsSuccess);
        Assert.Contains(GoodsIssue.FinalizedMessage, delete.Messages);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Finalization_RequiresExactlyOneTarget()
    {
        await using var context = new ApplicationTestContext();
        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(new SetGoodsIssueFinalizationCommand(true)));
        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(new SetGoodsIssueFinalizationCommand(true,
            Guid.NewGuid(), new DateOnly(2026, 10, 21), Guid.NewGuid())));
    }

    private static GoodsIssue Issue(string date) => new("OUT-1", DateTimeOffset.Parse(date), null, Guid.NewGuid());

    private static void Register(ApplicationTestContext context, params GoodsIssue[] issues)
    {
        var repository = Substitute.For<IRepositoryAsync<GoodsIssue, Guid>>();
        repository.Entities.Returns(issues.AsQueryable());
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => issues.SingleOrDefault(issue => issue.Id == call.Arg<Guid>()));
        repository.ToListAsync(Arg.Any<IQueryable<GoodsIssue>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<GoodsIssue>>().ToList());
        context.UnitOfWork.Repository<GoodsIssue>().Returns(repository);
    }
}
