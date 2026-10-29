using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptMetrics;

public sealed record GetGoodsReceiptMetricsQuery(
    bool IncludeMissingReceipt,
    bool IncludeMissingInvoice,
    bool IncludeTodayItems,
    bool IncludeTodayDocuments,
    DateTimeOffset UtcNow) : IRequest<Result<GoodsReceiptMetrics>>;

internal sealed class GetGoodsReceiptMetricsQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetGoodsReceiptMetricsQuery, Result<GoodsReceiptMetrics>>
{
    public async Task<Result<GoodsReceiptMetrics>> Handle(
        GetGoodsReceiptMetricsQuery request, CancellationToken cancellationToken)
    {
        var items = unitOfWork.Repository<GoodsReceiptItem>();
        var receipts = unitOfWork.Repository<GoodsReceipt>();
        int? missingReceipt = null;
        int? missingInvoice = null;
        int? todayItems = null;
        int? todayDocuments = null;
        int? todayCreatedItems=null;

        if (request.IncludeMissingReceipt)
            missingReceipt = await items.CountAsync(items.Entities.Where(item =>
                item.ReceiptNumber == null || item.ReceiptNumber == ""), cancellationToken);

        if (request.IncludeMissingInvoice)
            missingInvoice = await items.CountAsync(items.Entities.Where(item =>
                item.InvoiceStatus == null), cancellationToken);

        if (request.IncludeTodayItems || request.IncludeTodayDocuments)
        {
            var (startUtc, endUtc) = GoodsReceiptTodayRange.For(request.UtcNow);

            if (request.IncludeTodayItems)
                todayItems = await items.CountAsync(items.Entities.Where(item =>
                    item.GoodsReceipt.EnteredAtUtc >= startUtc &&
                    item.GoodsReceipt.EnteredAtUtc < endUtc), cancellationToken);

            if (request.IncludeTodayDocuments)
                todayDocuments = await receipts.CountAsync(receipts.Entities.Where(receipt =>
                    receipt.EnteredAtUtc >= startUtc && receipt.EnteredAtUtc < endUtc), cancellationToken);
        }


            return Result<GoodsReceiptMetrics>.Success(new(
                missingReceipt, missingInvoice, todayItems, todayDocuments));
    }
}
