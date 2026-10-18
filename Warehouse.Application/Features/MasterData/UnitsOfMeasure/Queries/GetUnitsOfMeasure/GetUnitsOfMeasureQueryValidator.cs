using FluentValidation;
using Warehouse.Application.Features.MasterData.Common;

namespace Warehouse.Application.Features.MasterData.UnitsOfMeasure.Queries.GetUnitsOfMeasure;

internal sealed class GetUnitsOfMeasureQueryValidator
    : AbstractValidator<GetUnitsOfMeasureQuery>
{
    public GetUnitsOfMeasureQueryValidator()
    {
        MasterDataValidationRules.AddTitledListRules(this);

        RuleFor(query => query.Title)
            .MaximumLength(MasterDataValidationRules.TitleMaxLength)
            .When(query => !string.IsNullOrWhiteSpace(query.Title));
        RuleFor(query => query.Description)
            .MaximumLength(MasterDataValidationRules.DescriptionMaxLength)
            .When(query => !string.IsNullOrWhiteSpace(query.Description));
    }
}
