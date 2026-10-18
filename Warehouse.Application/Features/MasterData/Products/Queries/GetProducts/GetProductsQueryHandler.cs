using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Products.Queries.GetProducts;

internal sealed class GetProductsQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetProductsQuery, Result<MasterDataListResult<ProductSummary>>>
{
    public async Task<Result<MasterDataListResult<ProductSummary>>> Handle(
        GetProductsQuery request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Product>();
        var query = repository.Entities;
        var totalCount = await repository.CountAsync(query, cancellationToken);
        var search = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(product =>
                product.Title.Contains(search) ||
                product.Code.Contains(search) ||
                product.UnitOfMeasure.Title.Contains(search) ||
                (product.Description != null && product.Description.Contains(search)));
        }

        var title = request.Title?.Trim();
        if (!string.IsNullOrWhiteSpace(title))
        {
            query = query.Where(product => product.Title.Contains(title));
        }

        var code = request.Code?.Trim();
        if (!string.IsNullOrWhiteSpace(code))
        {
            query = query.Where(product => product.Code.Contains(code));
        }

        var unitTitle = request.UnitOfMeasureTitle?.Trim();
        if (!string.IsNullOrWhiteSpace(unitTitle))
        {
            query = query.Where(product => product.UnitOfMeasure.Title.Contains(unitTitle));
        }

        var description = request.Description?.Trim();
        if (!string.IsNullOrWhiteSpace(description))
        {
            query = query.Where(product =>
                product.Description != null && product.Description.Contains(description));
        }

        var filteredCount = await repository.CountAsync(query, cancellationToken);
        query = ApplySorting(query, request.SortBy, request.SortDescending);

        if (request.PaginationEnabled)
        {
            query = query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize);
        }

        var items = await repository.ToListAsync(
            query.Select(product => new ProductSummary
            {
                Id = product.Id,
                Title = product.Title,
                Code = product.Code,
                UnitOfMeasureId = product.UnitOfMeasureId,
                UnitOfMeasureTitle = product.UnitOfMeasure.Title,
                Description = product.Description
            }),
            cancellationToken);

        var result = new MasterDataListResult<ProductSummary>(
            items,
            totalCount,
            filteredCount,
            request.PaginationEnabled ? request.PageNumber : 1,
            request.PaginationEnabled ? request.PageSize : items.Count,
            request.PaginationEnabled);

        return Result<MasterDataListResult<ProductSummary>>.Success(result);
    }

    private static IQueryable<Product> ApplySorting(
        IQueryable<Product> query,
        string? sortBy,
        bool descending) =>
        (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("id", false) => query.OrderBy(product => product.Id),
            ("id", true) => query.OrderByDescending(product => product.Id),
            ("code", false) => query.OrderBy(product => product.Code).ThenBy(product => product.Id),
            ("code", true) => query.OrderByDescending(product => product.Code)
                .ThenByDescending(product => product.Id),
            ("unitofmeasuretitle", false) => query.OrderBy(product => product.UnitOfMeasure.Title)
                .ThenBy(product => product.Title),
            ("unitofmeasuretitle", true) => query.OrderByDescending(product => product.UnitOfMeasure.Title)
                .ThenByDescending(product => product.Title),
            ("title", true) => query.OrderByDescending(product => product.Title)
                .ThenByDescending(product => product.Id),
            _ => query.OrderBy(product => product.Title).ThenBy(product => product.Id)
        };
}
