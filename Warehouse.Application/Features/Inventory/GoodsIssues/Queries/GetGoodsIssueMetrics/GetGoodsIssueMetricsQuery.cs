using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssueMetrics;

public sealed record GoodsIssueMetrics(int TodayItemCount, int TodayDocumentCount,
    int TodayCreatedItemCount, int TodayCreatedDocumentCount);
public sealed record GetGoodsIssueMetricsQuery : IRequest<Result<GoodsIssueMetrics>>;

internal sealed class GetGoodsIssueMetricsQueryHandler(IUnitOfWork<Guid> unitOfWork, TimeProvider clock)
    : IRequestHandler<GetGoodsIssueMetricsQuery, Result<GoodsIssueMetrics>>
{
    public async Task<Result<GoodsIssueMetrics>> Handle(GetGoodsIssueMetricsQuery request, CancellationToken token)
    {
        var (start, end) = InventoryTodayRange.For(clock.GetUtcNow());
        var issues = unitOfWork.Repository<GoodsIssue>();
        var items = unitOfWork.Repository<GoodsIssueItem>();
        var todayItems = await items.CountAsync(items.Entities.Where(item =>
            item.GoodsIssue.IssuedAtUtc >= start && item.GoodsIssue.IssuedAtUtc < end), token);
        var todayDocuments = await issues.CountAsync(issues.Entities.Where(issue =>
            issue.IssuedAtUtc >= start && issue.IssuedAtUtc < end), token);
        var createdItems = await items.CountAsync(items.Entities.Where(item =>
            item.CreatedOn >= start && item.CreatedOn < end), token);
        var createdDocuments = await issues.CountAsync(issues.Entities.Where(issue =>
            issue.CreatedOn >= start && issue.CreatedOn < end), token);
        return Result<GoodsIssueMetrics>.Success(new(todayItems, todayDocuments, createdItems, createdDocuments));
    }
}
