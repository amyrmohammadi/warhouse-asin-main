using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.UnitsOfMeasure.Commands.UpdateUnitOfMeasure;

internal sealed class UpdateUnitOfMeasureCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<UpdateUnitOfMeasureCommand, Result<TitledMasterDataSummary>>
{
    public async Task<Result<TitledMasterDataSummary>> Handle(
        UpdateUnitOfMeasureCommand request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<UnitOfMeasure>();
        var item = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (item is null)
        {
            return Result<TitledMasterDataSummary>.Error(
                "The requested unit of measure was not found.");
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
