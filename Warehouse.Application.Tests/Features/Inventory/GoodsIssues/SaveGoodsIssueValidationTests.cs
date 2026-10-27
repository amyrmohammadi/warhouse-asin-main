using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.GoodsIssues.Commands.SaveGoodsIssue;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsIssues;

public sealed class SaveGoodsIssueValidationTests
{
    [Fact]
    public async Task Send_WithoutItems_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(ValidCommand() with { Items = [] }));
    }

    [Fact]
    public async Task Send_WithoutProductCode_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var item = ValidItem() with { ProductId = null, ProductCode = "" };
        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(ValidCommand() with { Items = [item] }));
    }

    [Fact]
    public async Task Send_WithoutDestination_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var item = ValidItem() with { DestinationId = null, DestinationTitle = null };
        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(ValidCommand() with { Items = [item] }));
    }

    [Fact]
    public async Task Send_WithNonPositiveQuantity_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var item = ValidItem() with { Quantity = 0 };
        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(ValidCommand() with { Items = [item] }));
    }

    [Fact]
    public async Task Send_WithReturnDateBeforeIssueDate_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var command = ValidCommand();
        var item = ValidItem() with { ReturnAtUtc = command.IssuedAtUtc.AddDays(-1) };
        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(command with { Items = [item] }));
    }

    [Fact]
    public async Task Send_WithDescriptionLongerThanOneThousandCharacters_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(ValidCommand() with { Description = new string('ت', 1001) }));
    }

    [Fact]
    public async Task Send_UpdateWithStaleConcurrencyStamp_IsRejectedWithoutWriting()
    {
        await using var context = new ApplicationTestContext();
        var issue = new GoodsIssue("OUT-100", DateTimeOffset.UtcNow, null, Guid.NewGuid());
        var repository = Substitute.For<IRepositoryAsync<GoodsIssue, Guid>>();
        repository.GetByIdAsync(issue.Id, Arg.Any<CancellationToken>()).Returns(issue);
        context.UnitOfWork.Repository<GoodsIssue>().Returns(repository);
        var staleStamp = Guid.NewGuid();
        while (staleStamp == issue.ConcurrencyStamp) staleStamp = Guid.NewGuid();

        var result = await context.Sender.Send(ValidCommand() with
        {
            Id = issue.Id,
            ConcurrencyStamp = staleStamp
        });

        Assert.False(result.IsSuccess);
        Assert.Contains(ConcurrencyMessages.Conflict, result.Messages);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_UpdateWithLinkedReceiptItems_IsRejectedWithoutReplacingIssueItems()
    {
        await using var context = new ApplicationTestContext();
        var issue = new GoodsIssue("OUT-100", DateTimeOffset.UtcNow, null, Guid.NewGuid());
        var issueRepository = Substitute.For<IRepositoryAsync<GoodsIssue, Guid>>();
        issueRepository.GetByIdAsync(issue.Id, Arg.Any<CancellationToken>()).Returns(issue);
        context.UnitOfWork.Repository<GoodsIssue>().Returns(issueRepository);
        var itemRepository = Substitute.For<IRepositoryAsync<GoodsIssueItem, Guid>>();
        itemRepository.Entities.Returns(Array.Empty<GoodsIssueItem>().AsQueryable());
        itemRepository.CountAsync(Arg.Any<IQueryable<GoodsIssueItem>>(), Arg.Any<CancellationToken>())
            .Returns(1);
        context.UnitOfWork.Repository<GoodsIssueItem>().Returns(itemRepository);
        var stamp = issue.ConcurrencyStamp;

        var result = await context.Sender.Send(ValidCommand() with
        {
            Id = issue.Id,
            ConcurrencyStamp = stamp
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("ابتدا ارتباط آیتم‌های ورود کالا با این سند خروج را حذف کنید.", result.Messages);
        Assert.Equal(stamp, issue.ConcurrencyStamp);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
        await context.UnitOfWork.DidNotReceive().BeginTransaction(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TryAdvanceConcurrencyStamp_WithCurrentStamp_RotatesStampAndRejectsReuse()
    {
        var issue = new GoodsIssue("OUT-100", DateTimeOffset.UtcNow, null, Guid.NewGuid());
        var originalStamp = issue.ConcurrencyStamp;

        Assert.True(issue.TryAdvanceConcurrencyStamp(originalStamp));
        Assert.NotEqual(originalStamp, issue.ConcurrencyStamp);
        Assert.False(issue.TryAdvanceConcurrencyStamp(originalStamp));
    }

    private static SaveGoodsIssueCommand ValidCommand() => new(
        null, "OUT-100", DateTimeOffset.UtcNow, "توضیحات کلی", Guid.NewGuid(), null, [ValidItem()]);

    private static SaveGoodsIssueItem ValidItem() => new(
        Guid.NewGuid(), "P-100", 1, null, Guid.NewGuid(), null,
        "ارسال به پروژه", null, "واحد فناوری", null);
}
