using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.UnitsOfMeasure.Commands.CreateUnitOfMeasure;

internal sealed class CreateUnitOfMeasureCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<CreateUnitOfMeasureCommand, Result<TitledMasterDataSummary>>
{
    public async Task<Result<TitledMasterDataSummary>> Handle(
        CreateUnitOfMeasureCommand request,
        CancellationToken cancellationToken)
    {
        var item = new UnitOfMeasure(request.Title, request.Description);
        await unitOfWork.Repository<UnitOfMeasure>().AddAsync(item, cancellationToken);
        await unitOfWork.SaveAsync(cancellationToken);

        return Result<TitledMasterDataSummary>.Success(new TitledMasterDataSummary
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description
        });
    }
}
