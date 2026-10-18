using FluentValidation;
using Warehouse.Application.Features.MasterData.Common;

namespace Warehouse.Application.Features.MasterData.Products.Queries.GetProducts;

internal sealed class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsQueryValidator()
    {
        MasterDataValidationRules.AddProductListRules(this);

        RuleFor(query => query.Title)
            .MaximumLength(MasterDataValidationRules.TitleMaxLength)
            .When(query => !string.IsNullOrWhiteSpace(query.Title));
        RuleFor(query => query.Code)
            .MaximumLength(MasterDataValidationRules.ProductCodeMaxLength)
            .When(query => !string.IsNullOrWhiteSpace(query.Code));
        RuleFor(query => query.UnitOfMeasureTitle)
            .MaximumLength(MasterDataValidationRules.TitleMaxLength)
            .When(query => !string.IsNullOrWhiteSpace(query.UnitOfMeasureTitle));
        RuleFor(query => query.Description)
            .MaximumLength(MasterDataValidationRules.DescriptionMaxLength)
            .When(query => !string.IsNullOrWhiteSpace(query.Description));
    }
}
