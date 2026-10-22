using Warehouse.Application.Common.Models.Auditing;

namespace Warehouse.Application.Common.Interfaces;

public interface IAuditTrailService
{
    Task<GetAuditTrailsResult> GetAuditTrailsAsync(
        GetAuditTrailsRequest request,
        CancellationToken cancellationToken = default);

    Task<GoodsReceiptItemHistoryResult?> GetGoodsReceiptItemHistoryAsync(
        Guid itemId,
        CancellationToken cancellationToken = default);
}
