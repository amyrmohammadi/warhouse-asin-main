using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Warehouses.Queries.GetWarehouseById;

internal sealed class GetWarehouseByIdQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetWarehouseByIdQuery, Result<TitledMasterDataSummary>>
{
    public async Task<Result<TitledMasterDataSummary>> Handle(
        GetWarehouseByIdQuery request,
        CancellationToken cancellationToken)
    {
        var item = await unitOfWork
            .Repository<StorageWarehouse>()
            .GetByIdAsync(request.Id, cancellationToken);

        return item is null
            ? Result<TitledMasterDataSummary>.Error("The requested warehouse was not found.")
            : Result<TitledMasterDataSummary>.Success(new TitledMasterDataSummary
            {
                Id = item.Id,
                ConcurrencyStamp = item.ConcurrencyStamp,
                Title = item.Title,
                Description = item.Description
            });
    }
}
