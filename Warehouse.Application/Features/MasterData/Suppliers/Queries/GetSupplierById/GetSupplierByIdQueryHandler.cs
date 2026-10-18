using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Suppliers.Queries.GetSupplierById;

internal sealed class GetSupplierByIdQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetSupplierByIdQuery, Result<TitledMasterDataSummary>>
{
    public async Task<Result<TitledMasterDataSummary>> Handle(
        GetSupplierByIdQuery request,
        CancellationToken cancellationToken)
    {
        var item = await unitOfWork
            .Repository<Supplier>()
            .GetByIdAsync(request.Id, cancellationToken);

        return item is null
            ? Result<TitledMasterDataSummary>.Error("The requested supplier was not found.")
            : Result<TitledMasterDataSummary>.Success(new TitledMasterDataSummary
            {
                Id = item.Id,
                Title = item.Title,
                Description = item.Description
            });
    }
}
