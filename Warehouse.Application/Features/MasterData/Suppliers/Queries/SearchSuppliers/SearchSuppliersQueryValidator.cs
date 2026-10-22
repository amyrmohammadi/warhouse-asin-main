using FluentValidation;

namespace Warehouse.Application.Features.MasterData.Suppliers.Queries.SearchSuppliers;

internal sealed class SearchSuppliersQueryValidator : AbstractValidator<SearchSuppliersQuery>
{
    public SearchSuppliersQueryValidator()
    {
        RuleFor(query => query.Search)
            .NotEmpty()
            .WithMessage("عبارت جستجو الزامی است.")
            .MinimumLength(SearchSuppliersQuery.MinimumSearchLength)
            .WithMessage(
                $"عبارت جستجو باید حداقل {SearchSuppliersQuery.MinimumSearchLength} نویسه باشد.")
            .MaximumLength(200)
            .WithMessage("عبارت جستجو نباید بیشتر از ۲۰۰ نویسه باشد.");
        RuleFor(query => query.Limit)
            .InclusiveBetween(1, SearchSuppliersQuery.MaximumResults)
            .WithMessage(
                $"تعداد نتایج باید بین ۱ و {SearchSuppliersQuery.MaximumResults} باشد.");
    }
}
