using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsIssues.Queries.GetGoodsIssueById;

public sealed record GetGoodsIssueByIdQuery(Guid Id) : IRequest<Result<GoodsIssueSummary>>;

internal sealed class GetGoodsIssueByIdQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetGoodsIssueByIdQuery, Result<GoodsIssueSummary>>
{
    public async Task<Result<GoodsIssueSummary>> Handle(
        GetGoodsIssueByIdQuery request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<GoodsIssue>();
        var item = (await repository.ToListAsync(
            repository.Entities.Where(issue => issue.Id == request.Id).ToSummary(),
            cancellationToken)).FirstOrDefault();
        return item is null
            ? Result<GoodsIssueSummary>.Error("سند خروج کالا پیدا نشد.")
            : Result<GoodsIssueSummary>.Success(item);
    }
}
