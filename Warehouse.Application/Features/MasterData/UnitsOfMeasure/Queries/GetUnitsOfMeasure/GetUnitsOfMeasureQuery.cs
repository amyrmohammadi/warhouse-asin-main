using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.UnitsOfMeasure.Queries.GetUnitsOfMeasure;

public sealed record GetUnitsOfMeasureQuery
    : DataTableParameters, IRequest<Result<MasterDataListResult<TitledMasterDataSummary>>>
{
    public string? Title { get; init; }

    public string? Description { get; init; }
}
