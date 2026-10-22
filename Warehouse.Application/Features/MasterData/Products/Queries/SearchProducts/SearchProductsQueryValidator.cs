using FluentValidation;

namespace Warehouse.Application.Features.MasterData.Products.Queries.SearchProducts;

internal sealed class SearchProductsQueryValidator : AbstractValidator<SearchProductsQuery>
{
    public SearchProductsQueryValidator()
    {
        RuleFor(query => query.Search)
            .NotEmpty()
            .WithMessage("عبارت جستجو الزامی است.")
            .MinimumLength(SearchProductsQuery.MinimumSearchLength)
            .WithMessage(
                $"عبارت جستجو باید حداقل {SearchProductsQuery.MinimumSearchLength} نویسه باشد.")
            .MaximumLength(200)
            .WithMessage("عبارت جستجو نباید بیشتر از ۲۰۰ نویسه باشد.");
        RuleFor(query => query.Limit)
            .InclusiveBetween(1, SearchProductsQuery.MaximumResults)
            .WithMessage(
                $"تعداد نتایج باید بین ۱ و {SearchProductsQuery.MaximumResults} باشد.");
    }
}
