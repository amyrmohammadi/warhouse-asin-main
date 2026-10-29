using Warehouse.Application.Common.Models.Inventory;

namespace Warehouse.Application.Common.Interfaces;

public interface IGoodsIssuePdfService
{
    Task<byte[]> GenerateAsync(GoodsIssueSummary issue, CancellationToken cancellationToken = default);
}
