using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Warehouses.Commands.CreateWarehouse;

internal sealed class CreateWarehouseCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<CreateWarehouseCommand, Result<TitledMasterDataSummary>>
{
    public async Task<Result<TitledMasterDataSummary>> Handle(
        CreateWarehouseCommand request,
        CancellationToken cancellationToken)
    {
        var item = new StorageWarehouse(request.Title, request.Description);
        await unitOfWork.Repository<StorageWarehouse>().AddAsync(item, cancellationToken);
        await unitOfWork.SaveAsync(cancellationToken);

        return Result<TitledMasterDataSummary>.Success(new TitledMasterDataSummary
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description
        });
    }
}
