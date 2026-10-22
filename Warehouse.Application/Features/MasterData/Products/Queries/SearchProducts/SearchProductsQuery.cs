using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.Products.Queries.SearchProducts;

public sealed record SearchProductsQuery
    : IRequest<Result<IReadOnlyCollection<ProductSummary>>>
{
    public const int MinimumSearchLength = 2;
    public const int MaximumResults = 30;

    public string Search { get; init; } = string.Empty;

    public int Limit { get; init; } = MaximumResults;
}
