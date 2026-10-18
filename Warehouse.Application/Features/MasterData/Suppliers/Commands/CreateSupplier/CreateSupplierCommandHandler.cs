using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Suppliers.Commands.CreateSupplier;

internal sealed class CreateSupplierCommandHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<CreateSupplierCommand, Result<TitledMasterDataSummary>>
{
    public async Task<Result<TitledMasterDataSummary>> Handle(
        CreateSupplierCommand request,
        CancellationToken cancellationToken)
    {
        var item = new Supplier(request.Title, request.Description);
        await unitOfWork.Repository<Supplier>().AddAsync(item, cancellationToken);
        await unitOfWork.SaveAsync(cancellationToken);

        return Result<TitledMasterDataSummary>.Success(new TitledMasterDataSummary
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description
        });
    }
}
