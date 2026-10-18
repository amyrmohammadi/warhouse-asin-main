using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.UnitsOfMeasure.Commands.DeleteUnitOfMeasure;

internal sealed class DeleteUnitOfMeasureCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<DeleteUnitOfMeasureCommand, Result>
{
    public async Task<Result> Handle(
        DeleteUnitOfMeasureCommand request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<UnitOfMeasure>();
        var item = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (item is null)
        {
            return Result.Error("The requested unit of measure was not found.");
        }

        var products = unitOfWork.Repository<Product>();
        if (await products.CountAsync(
                products.Entities.Where(product => product.UnitOfMeasureId == request.Id),
                cancellationToken) > 0)
        {
            return Result.Error("این واحد اندازه‌گیری در تعریف کالا استفاده شده و قابل حذف نیست.");
        }

        await repository.DeleteAsync(item, cancellationToken);
        await unitOfWork.SaveAsync(cancellationToken);
        return Result.Success();
    }
}
