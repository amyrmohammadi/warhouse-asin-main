using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.Suppliers.Queries.GetSuppliers;

public sealed record GetSuppliersQuery
    : DataTableParameters, IRequest<Result<MasterDataListResult<TitledMasterDataSummary>>>
{
    public string? Title { get; init; }

    public string? Description { get; init; }
}
