using FluentValidation;

namespace Warehouse.Application.Features.MasterData.UnitsOfMeasure.Queries.GetUnitOfMeasureById;

internal sealed class GetUnitOfMeasureByIdQueryValidator
    : AbstractValidator<GetUnitOfMeasureByIdQuery>
{
    public GetUnitOfMeasureByIdQueryValidator() =>
        RuleFor(query => query.Id).NotEmpty();
}
