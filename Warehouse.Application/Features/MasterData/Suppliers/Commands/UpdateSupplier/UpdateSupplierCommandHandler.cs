using MediatR;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Suppliers.Commands.UpdateSupplier;

internal sealed class UpdateSupplierCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<UpdateSupplierCommand, Result<TitledMasterDataSummary>>
{
    public async Task<Result<TitledMasterDataSummary>> Handle(
        UpdateSupplierCommand request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Supplier>();
        var item = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (item is null)
        {
            return Result<TitledMasterDataSummary>.Error(
                "The requested supplier was not found.");
        }

        if (!item.TryAdvanceConcurrencyStamp(request.ConcurrencyStamp))
        {
            return Result<TitledMasterDataSummary>.Error(ConcurrencyMessages.Conflict);
        }

        item.Update(request.Title, request.Description);
        await repository.UpdateAsync(item, cancellationToken);
        await unitOfWork.SaveAsync(cancellationToken);

        return Result<TitledMasterDataSummary>.Success(new TitledMasterDataSummary
        {
            Id = item.Id,
            ConcurrencyStamp = item.ConcurrencyStamp,
            Title = item.Title,
            Description = item.Description
        });
    }
}
