using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.Suppliers.Queries.SearchSuppliers;

internal sealed class SearchSuppliersQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<SearchSuppliersQuery, Result<IReadOnlyCollection<TitledMasterDataSummary>>>
{
    public async Task<Result<IReadOnlyCollection<TitledMasterDataSummary>>> Handle(
        SearchSuppliersQuery request,
        CancellationToken cancellationToken)
    {
        var search = request.Search.Trim();
        var limit = Math.Clamp(request.Limit, 1, SearchSuppliersQuery.MaximumResults);

        var repository = unitOfWork.Repository<Supplier>();
        var items = await repository.ToListAsync(
            repository.Entities
                .Where(item => item.Title.Contains(search))
                .OrderBy(item => item.Title)
                .ThenBy(item => item.Id)
                .Take(limit)
                .Select(item => new TitledMasterDataSummary
                {
                    Id = item.Id,
                    ConcurrencyStamp = item.ConcurrencyStamp,
                    Title = item.Title,
                    Description = item.Description
                }),
            cancellationToken);

        return Result<IReadOnlyCollection<TitledMasterDataSummary>>.Success(items);
    }
}
