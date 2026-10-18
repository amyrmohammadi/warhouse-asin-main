using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.Products.Queries.GetProducts;

public sealed record GetProductsQuery
    : DataTableParameters, IRequest<Result<MasterDataListResult<ProductSummary>>>
{
    public string? Title { get; init; }

    public string? Code { get; init; }

    public string? UnitOfMeasureTitle { get; init; }

    public string? Description { get; init; }
}
