using FluentValidation;
using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.GoodsIssueDestinations.Queries.SearchGoodsIssueDestinations;

public sealed record SearchGoodsIssueDestinationsQuery
    : IRequest<Result<IReadOnlyCollection<TitledMasterDataSummary>>>
{
    public const int MinimumSearchLength = 2;
    public const int MaximumResults = 30;

    public string Search { get; init; } = string.Empty;
    public int Limit { get; init; } = MaximumResults;
}

internal sealed class SearchGoodsIssueDestinationsQueryValidator
    : AbstractValidator<SearchGoodsIssueDestinationsQuery>
{
    public SearchGoodsIssueDestinationsQueryValidator()
    {
        RuleFor(query => query.Search)
            .NotEmpty().WithMessage("عبارت جستجو الزامی است.")
            .MinimumLength(SearchGoodsIssueDestinationsQuery.MinimumSearchLength)
            .WithMessage($"عبارت جستجو باید حداقل {SearchGoodsIssueDestinationsQuery.MinimumSearchLength} نویسه باشد.")
            .MaximumLength(200).WithMessage("عبارت جستجو نباید بیشتر از ۲۰۰ نویسه باشد.");
        RuleFor(query => query.Limit)
            .InclusiveBetween(1, SearchGoodsIssueDestinationsQuery.MaximumResults)
            .WithMessage($"تعداد نتایج باید بین ۱ و {SearchGoodsIssueDestinationsQuery.MaximumResults} باشد.");
    }
}

internal sealed class SearchGoodsIssueDestinationsQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<SearchGoodsIssueDestinationsQuery, Result<IReadOnlyCollection<TitledMasterDataSummary>>>
{
    public async Task<Result<IReadOnlyCollection<TitledMasterDataSummary>>> Handle(
        SearchGoodsIssueDestinationsQuery request,
        CancellationToken cancellationToken)
    {
        var search = request.Search.Trim();
        var limit = Math.Clamp(request.Limit, 1, SearchGoodsIssueDestinationsQuery.MaximumResults);
        var repository = unitOfWork.Repository<GoodsIssueDestination>();
        var items = await repository.ToListAsync(
            repository.Entities
                .Where(item => item.Title.Contains(search))
                .OrderBy(item => item.Title)
                .ThenBy(item => item.Id)
                .Take(limit)
                .Select(item => new TitledMasterDataSummary
                {
                    Id = item.Id,
                    Title = item.Title,
                    Description = item.Description
                }),
            cancellationToken);

        return Result<IReadOnlyCollection<TitledMasterDataSummary>>.Success(items);
    }
}
