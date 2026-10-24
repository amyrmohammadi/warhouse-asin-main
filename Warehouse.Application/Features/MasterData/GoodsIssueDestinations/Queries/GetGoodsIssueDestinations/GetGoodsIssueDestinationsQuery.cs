using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Application.Features.MasterData.Common;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.GoodsIssueDestinations.Queries.GetGoodsIssueDestinations;

public sealed record GetGoodsIssueDestinationsQuery
    : DataTableParameters, IRequest<Result<MasterDataListResult<TitledMasterDataSummary>>>
{
    public string? Title { get; init; }
    public string? Description { get; init; }
}

internal sealed class GetGoodsIssueDestinationsQueryValidator : AbstractValidator<GetGoodsIssueDestinationsQuery>
{
    public GetGoodsIssueDestinationsQueryValidator() => MasterDataValidationRules.AddTitledListRules(this);
}

internal sealed class GetGoodsIssueDestinationsQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetGoodsIssueDestinationsQuery, Result<MasterDataListResult<TitledMasterDataSummary>>>
{
    public async Task<Result<MasterDataListResult<TitledMasterDataSummary>>> Handle(
        GetGoodsIssueDestinationsQuery request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<GoodsIssueDestination>();
        var query = repository.Entities;
        var totalCount = await repository.CountAsync(query, cancellationToken);
        var search = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(item => item.Title.Contains(search) ||
                (item.Description != null && item.Description.Contains(search)));
        if (!string.IsNullOrWhiteSpace(request.Title))
            query = query.Where(item => item.Title.Contains(request.Title.Trim()));
        if (!string.IsNullOrWhiteSpace(request.Description))
            query = query.Where(item => item.Description != null && item.Description.Contains(request.Description.Trim()));
        var filteredCount = await repository.CountAsync(query, cancellationToken);
        query = request.SortDescending
            ? query.OrderByDescending(item => item.Title).ThenByDescending(item => item.Id)
            : query.OrderBy(item => item.Title).ThenBy(item => item.Id);
        if (request.PaginationEnabled)
            query = query.Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize);
        var items = await repository.ToListAsync(query.Select(item => new TitledMasterDataSummary
        {
            Id = item.Id,
            ConcurrencyStamp = item.ConcurrencyStamp,
            Title = item.Title,
            Description = item.Description
        }), cancellationToken);
        return Result<MasterDataListResult<TitledMasterDataSummary>>.Success(new(
            items, totalCount, filteredCount,
            request.PaginationEnabled ? request.PageNumber : 1,
            request.PaginationEnabled ? request.PageSize : items.Count,
            request.PaginationEnabled));
    }
}
