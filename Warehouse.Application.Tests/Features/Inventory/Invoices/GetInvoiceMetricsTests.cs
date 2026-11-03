using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoiceMetrics;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.Invoices;

public sealed class GetInvoiceMetricsTests
{
    [Theory]
    [InlineData(20, 29, 3)]
    [InlineData(20, 30, 4)]
    public async Task Metrics_CountsOnlyMatchingDeliveryDatesAndStatuses_UsingTehranToday(int hour, int minute, int day)
    {
        await using var context = new ApplicationTestContext(new Clock(hour, minute));
        var third = new DateOnly(2026, 11, 3);
        var fourth = third.AddDays(1);
        var invoices = new[]
        {
            Create(third, InvoiceProcessingStatus.SentToFinance),
            Create(third, InvoiceProcessingStatus.SentToFinance),
            Create(third, InvoiceProcessingStatus.UnderReview),
            Create(third, InvoiceProcessingStatus.FinanceApproved),
            Create(fourth, InvoiceProcessingStatus.SentToFinance),
            Create(fourth, InvoiceProcessingStatus.UnderReview),
            Create(fourth, InvoiceProcessingStatus.UnderReview),
            Create(null, InvoiceProcessingStatus.SentToFinance),
            Create(third.AddDays(-1), InvoiceProcessingStatus.SentToFinance)
        };
        var repository = Substitute.For<IRepositoryAsync<Invoice, Guid>>();
        repository.Entities.Returns(invoices.AsQueryable());
        repository.CountAsync(Arg.Any<IQueryable<Invoice>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<Invoice>>().Count());
        context.UnitOfWork.Repository<Invoice>().Returns(repository);
        var items = Substitute.For<IRepositoryAsync<GoodsReceiptItem, Guid>>();
        items.Entities.Returns(Array.Empty<GoodsReceiptItem>().AsQueryable());
        items.CountAsync(Arg.Any<IQueryable<GoodsReceiptItem>>(), Arg.Any<CancellationToken>()).Returns(0);
        context.UnitOfWork.Repository<GoodsReceiptItem>().Returns(items);

        var result = await context.Sender.Send(new GetInvoiceMetricsQuery());
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 11, day), result.Data!.Today);
        Assert.Equal(day == 3 ? 2 : 1, result.Data.SentToFinanceTodayCount);
        Assert.Equal(day == 3 ? 1 : 2, result.Data.UnderReviewTodayCount);
    }

    private static Invoice Create(DateOnly? date, InvoiceProcessingStatus status)
    {
        var invoice = new Invoice(Guid.NewGuid().ToString(), null);
        invoice.UpdateFinanceDeliveryDate(date);
        invoice.UpdateDetails(null, null, status);
        return invoice;
    }

    private sealed class Clock(int hour, int minute) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 3, hour, minute, 0, TimeSpan.Zero);
    }
}
