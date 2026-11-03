using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoiceMetrics;

public sealed record InvoiceMetrics(DateOnly Today, int SentToFinanceTodayCount, int UnderReviewTodayCount,
    int TodayInvoiceCount = 0, int TodayItemCount = 0, int TodayCreatedInvoiceCount = 0, int TodayCreatedItemCount = 0);
public sealed record GetInvoiceMetricsQuery : IRequest<Result<InvoiceMetrics>>;

internal sealed class GetInvoiceMetricsQueryHandler(IUnitOfWork<Guid> unitOfWork, TimeProvider timeProvider)
    : IRequestHandler<GetInvoiceMetricsQuery, Result<InvoiceMetrics>>
{
    public async Task<Result<InvoiceMetrics>> Handle(GetInvoiceMetricsQuery request, CancellationToken token)
    {
        var now = timeProvider.GetUtcNow();
        var today = Invoice.GetReceiptDate(now);
        var repository = unitOfWork.Repository<Invoice>();
        var query = repository.Entities.Where(invoice => invoice.FinanceDeliveryDate == today);
        var sent = await repository.CountAsync(query.Where(invoice =>
            invoice.ProcessingStatus == InvoiceProcessingStatus.SentToFinance), token);
        var underReview = await repository.CountAsync(query.Where(invoice =>
            invoice.ProcessingStatus == InvoiceProcessingStatus.UnderReview), token);
        var (start, end) = InventoryTodayRange.For(now);
        var todayInvoices = await repository.CountAsync(repository.Entities.Where(invoice => invoice.InvoiceDate == today), token);
        var createdInvoices = await repository.CountAsync(repository.Entities.Where(invoice =>
            invoice.CreatedOn >= start && invoice.CreatedOn < end), token);
        var items = unitOfWork.Repository<GoodsReceiptItem>();
        var todayItems = await items.CountAsync(items.Entities.Where(item => item.Invoice != null && item.Invoice.InvoiceDate == today), token);
        var createdItems = await items.CountAsync(items.Entities.Where(item =>
            item.InvoiceId.HasValue && item.CreatedOn >= start && item.CreatedOn < end), token);
        return Result<InvoiceMetrics>.Success(new(today, sent, underReview, todayInvoices, todayItems, createdInvoices, createdItems));
    }
}
