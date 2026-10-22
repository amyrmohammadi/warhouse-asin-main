using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Auditing;

namespace Warehouse.Application.Features.Auditing.Queries.GetGoodsReceiptItemHistory;

public sealed record GetGoodsReceiptItemHistoryQuery(Guid ItemId)
    : IRequest<Result<GoodsReceiptItemHistoryResult>>;

internal sealed class GetGoodsReceiptItemHistoryQueryHandler(IAuditTrailService auditTrailService)
    : IRequestHandler<GetGoodsReceiptItemHistoryQuery, Result<GoodsReceiptItemHistoryResult>>
{
    public async Task<Result<GoodsReceiptItemHistoryResult>> Handle(
        GetGoodsReceiptItemHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var history = await auditTrailService.GetGoodsReceiptItemHistoryAsync(
            request.ItemId,
            cancellationToken);

        return history is null
            ? Result<GoodsReceiptItemHistoryResult>.Error("ردیف کالای ورودی پیدا نشد.")
            : Result<GoodsReceiptItemHistoryResult>.Success(history);
    }
}
