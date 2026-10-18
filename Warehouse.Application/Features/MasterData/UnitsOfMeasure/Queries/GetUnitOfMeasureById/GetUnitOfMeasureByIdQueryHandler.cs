using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.UnitsOfMeasure.Queries.GetUnitOfMeasureById;

internal sealed class GetUnitOfMeasureByIdQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetUnitOfMeasureByIdQuery, Result<TitledMasterDataSummary>>
{
    public async Task<Result<TitledMasterDataSummary>> Handle(
        GetUnitOfMeasureByIdQuery request,
        CancellationToken cancellationToken)
    {
        var item = await unitOfWork
            .Repository<UnitOfMeasure>()
            .GetByIdAsync(request.Id, cancellationToken);

        return item is null
            ? Result<TitledMasterDataSummary>.Error("The requested unit of measure was not found.")
            : Result<TitledMasterDataSummary>.Success(new TitledMasterDataSummary
            {
                Id = item.Id,
                Title = item.Title,
                Description = item.Description
            });
    }
}
