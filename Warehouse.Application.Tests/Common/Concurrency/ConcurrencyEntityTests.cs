using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.MasterData.UnitsOfMeasure.Commands.UpdateUnitOfMeasure;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Tests.Common.Concurrency;

public sealed class ConcurrencyEntityTests
{
    [Fact]
    public void EveryEditableRecord_RotatesItsStamp_AndRejectsAReusedStamp()
    {
        ConcurrencyEntity<Guid>[] records =
        [
            new GoodsReceipt(DateTimeOffset.UtcNow, null, Guid.NewGuid(), "1"),
            new GoodsIssue("1", DateTimeOffset.UtcNow, null, Guid.NewGuid()),
            new UnitOfMeasure("عدد", null),
            new Supplier("تأمین‌کننده", null),
            new StorageWarehouse("انبار", null),
            new Person("تحویل‌دهنده", ""),
            new Product("کالا", "001", Guid.NewGuid(), null),
            new GoodsIssueDestination("مقصد", null)
        ];

        foreach (var record in records)
        {
            var originalStamp = record.ConcurrencyStamp;

            Assert.NotEqual(Guid.Empty, originalStamp);
            Assert.True(record.TryAdvanceConcurrencyStamp(originalStamp));
            Assert.NotEqual(originalStamp, record.ConcurrencyStamp);

            var currentStamp = record.ConcurrencyStamp;
            Assert.False(record.TryAdvanceConcurrencyStamp(originalStamp));
            Assert.Equal(currentStamp, record.ConcurrencyStamp);
        }
    }

    [Fact]
    public async Task MasterDataUpdate_WithStaleStamp_IsRejectedWithoutWriting()
    {
        await using var context = new ApplicationTestContext();
        var unit = new UnitOfMeasure("عدد", null);
        var repository = Substitute.For<IRepositoryAsync<UnitOfMeasure, Guid>>();
        repository.GetByIdAsync(unit.Id, Arg.Any<CancellationToken>()).Returns(unit);
        context.UnitOfWork.Repository<UnitOfMeasure>().Returns(repository);
        var staleStamp = Guid.NewGuid();
        while (staleStamp == unit.ConcurrencyStamp) staleStamp = Guid.NewGuid();

        var result = await context.Sender.Send(new UpdateUnitOfMeasureCommand(
            unit.Id,
            "کیلوگرم",
            null,
            staleStamp));

        Assert.False(result.IsSuccess);
        Assert.Contains(ConcurrencyMessages.Conflict, result.Messages);
        Assert.Equal("عدد", unit.Title);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }
}
