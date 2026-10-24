using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Warehouses.Queries.GetWarehouses;

internal sealed class GetWarehousesQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetWarehousesQuery, Result<MasterDataListResult<TitledMasterDataSummary>>>
{
    public async Task<Result<MasterDataListResult<TitledMasterDataSummary>>> Handle(
        GetWarehousesQuery request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<StorageWarehouse>();
        var query = repository.Entities;
        var totalCount = await repository.CountAsync(query, cancellationToken);
        var search = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(item =>
                item.Title.Contains(search) ||
                (item.Description != null && item.Description.Contains(search)));
        }

        var title = request.Title?.Trim();
        if (!string.IsNullOrWhiteSpace(title))
        {
            query = query.Where(item => item.Title.Contains(title));
        }

        var description = request.Description?.Trim();
        if (!string.IsNullOrWhiteSpace(description))
        {
            query = query.Where(item =>
                item.Description != null && item.Description.Contains(description));
        }

        var filteredCount = await repository.CountAsync(query, cancellationToken);
        query = ApplySorting(query, request.SortBy, request.SortDescending);

        if (request.PaginationEnabled)
        {
            query = query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize);
        }

        var page = await repository.ToListAsync(query, cancellationToken);
        var items = page
            .Select(item => new TitledMasterDataSummary
            {
                Id = item.Id,
                ConcurrencyStamp = item.ConcurrencyStamp,
                Title = item.Title,
                Description = item.Description
            })
            .ToArray();
        var result = new MasterDataListResult<TitledMasterDataSummary>(
            items,
            totalCount,
            filteredCount,
            request.PaginationEnabled ? request.PageNumber : 1,
            request.PaginationEnabled ? request.PageSize : items.Length,
            request.PaginationEnabled);

        return Result<MasterDataListResult<TitledMasterDataSummary>>.Success(result);
    }

    private static IQueryable<StorageWarehouse> ApplySorting(
        IQueryable<StorageWarehouse> query,
        string? sortBy,
        bool descending) =>
        (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("id", false) => query.OrderBy(item => item.Id),
            ("id", true) => query.OrderByDescending(item => item.Id),
            ("title", true) => query.OrderByDescending(item => item.Title)
                .ThenByDescending(item => item.Id),
            _ => query.OrderBy(item => item.Title).ThenBy(item => item.Id)
        };
}
