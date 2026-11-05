using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetNextGoodsIssueNumber;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsIssues;

public sealed class NextGoodsIssueNumberTests
{
    [Theory]
    [InlineData("", "99/00001")]
    [InlineData("99/9;99/10;99/2", "99/00011")]
    [InlineData("99/0009;99/0010", "99/00011")]
    [InlineData("99/3795", "99/03796")]
    [InlineData("99/03795", "99/03796")]
    [InlineData("99/999999;99/7", "99/1000000")]
    [InlineData("۹۹/۱۲;99/9;٩٩/١٣", "99/00014")]
    [InlineData("OUT-999;98/1000;99/invalid;99/-7;99/1/2;99/", "99/00001")]
    public async Task SuggestsNextNumericSuffix_WithoutPersistingOrReserving(string existing, string expected)
    {
        await using var context = new ApplicationTestContext();
        var repository = Substitute.For<IRepositoryAsync<GoodsIssue, Guid>>();
        repository.Entities.Returns(existing.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(number => new GoodsIssue(number, DateTimeOffset.UtcNow, null, Guid.NewGuid())).AsQueryable());
        repository.ToListAsync(Arg.Any<IQueryable<string>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<string>>().ToArray());
        context.UnitOfWork.Repository<GoodsIssue>().Returns(repository);

        var first = await context.Sender.Send(new GetNextGoodsIssueNumberQuery());
        var second = await context.Sender.Send(new GetNextGoodsIssueNumberQuery());

        Assert.True(first.IsSuccess);
        Assert.Equal(expected, first.Data);
        Assert.Equal(first.Data, second.Data);
        Assert.DoesNotContain(repository.ReceivedCalls(), call => call.GetMethodInfo().Name is "AddAsync" or "UpdateAsync" or "DeleteAsync");
        Assert.DoesNotContain(context.UnitOfWork.ReceivedCalls(), call => call.GetMethodInfo().Name is "SaveAsync" or "BeginTransaction" or "CommitTransaction");
    }
}
