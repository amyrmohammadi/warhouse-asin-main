using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Warehouses.Commands.UpdateWarehouse;

internal sealed class UpdateWarehouseCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<UpdateWarehouseCommand, Result<TitledMasterDataSummary>>
{
    public async Task<Result<TitledMasterDataSummary>> Handle(
        UpdateWarehouseCommand request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<StorageWarehouse>();
        var item = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (item is null)
        {
            return Result<TitledMasterDataSummary>.Error(
                "The requested warehouse was not found.");
        }

        item.Update(request.Title, request.Description);
        await repository.UpdateAsync(item, cancellationToken);
        await unitOfWork.SaveAsync(cancellationToken);

        return Result<TitledMasterDataSummary>.Success(new TitledMasterDataSummary
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description
        });
    }
}
