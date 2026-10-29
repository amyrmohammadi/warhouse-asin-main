using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsIssues;

internal static class GoodsIssueEditGuard
{
    public static async Task<bool> CanChangeItemAsync(
        IUnitOfWork<Guid> unitOfWork, GoodsIssueItem item, CancellationToken token)
    {
        var issue = await unitOfWork.Repository<GoodsIssue>().GetByIdAsync(item.GoodsIssueId, token);
        if (issue is null || issue.IsFinalized) return false;
        // A concurrent finalization must also invalidate updates made through a linked receipt.
        return issue.TryAdvanceConcurrencyStamp(issue.ConcurrencyStamp);
    }
}
