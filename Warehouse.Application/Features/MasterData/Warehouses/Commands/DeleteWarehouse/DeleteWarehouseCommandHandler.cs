using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Warehouses.Commands.DeleteWarehouse;

internal sealed class DeleteWarehouseCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<DeleteWarehouseCommand, Result>
{
    public async Task<Result> Handle(
        DeleteWarehouseCommand request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<StorageWarehouse>();
        var item = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (item is null)
        {
            return Result.Error("The requested warehouse was not found.");
        }

        if (await repository.CountAsync(
                repository.Entities.Where(warehouse =>
                    warehouse.Id == request.Id && warehouse.GoodsReceipts.Any()),
                cancellationToken) > 0)
        {
            return Result.Error("این انبار در تاریخچه ورود کالا استفاده شده و قابل حذف نیست.");
        }

        await repository.DeleteAsync(item, cancellationToken);
        await unitOfWork.SaveAsync(cancellationToken);
        return Result.Success();
    }
}
