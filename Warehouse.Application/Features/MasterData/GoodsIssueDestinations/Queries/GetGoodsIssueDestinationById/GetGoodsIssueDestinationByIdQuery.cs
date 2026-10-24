using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.GoodsIssueDestinations.Queries.GetGoodsIssueDestinationById;

public sealed record GetGoodsIssueDestinationByIdQuery(Guid Id) : IRequest<Result<TitledMasterDataSummary>>;

internal sealed class GetGoodsIssueDestinationByIdQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetGoodsIssueDestinationByIdQuery, Result<TitledMasterDataSummary>>
{
    public async Task<Result<TitledMasterDataSummary>> Handle(
        GetGoodsIssueDestinationByIdQuery request,
        CancellationToken cancellationToken)
    {
        var item = await unitOfWork.Repository<GoodsIssueDestination>().GetByIdAsync(request.Id, cancellationToken);
        return item is null
            ? Result<TitledMasterDataSummary>.Error("مقصد پیدا نشد.")
            : Result<TitledMasterDataSummary>.Success(new TitledMasterDataSummary
            {
                Id = item.Id,
                ConcurrencyStamp = item.ConcurrencyStamp,
                Title = item.Title,
                Description = item.Description
            });
    }
}
