using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Products.Queries.SearchProducts;

internal sealed class SearchProductsQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<SearchProductsQuery, Result<IReadOnlyCollection<ProductSummary>>>
{
    public async Task<Result<IReadOnlyCollection<ProductSummary>>> Handle(
        SearchProductsQuery request,
        CancellationToken cancellationToken)
    {
        var search = request.Search.Trim();
        var limit = Math.Clamp(request.Limit, 1, SearchProductsQuery.MaximumResults);

        var productRepository = unitOfWork.Repository<Product>();
        var unitRepository = unitOfWork.Repository<UnitOfMeasure>();

        var query =
            from product in productRepository.Entities
            join unit in unitRepository.Entities on product.UnitOfMeasureId equals unit.Id
            where product.Title.Contains(search) ||
                  product.Code.Contains(search) ||
                  unit.Title.Contains(search)
            orderby product.Title, product.Id
            select new ProductSummary
            {
                Id = product.Id,
                ConcurrencyStamp = product.ConcurrencyStamp,
                Title = product.Title,
                Code = product.Code,
                UnitOfMeasureId = product.UnitOfMeasureId,
                UnitOfMeasureTitle = unit.Title,
                Description = product.Description
            };

        var items = await productRepository.ToListAsync(query.Take(limit), cancellationToken);

        return Result<IReadOnlyCollection<ProductSummary>>.Success(items);
    }
}
