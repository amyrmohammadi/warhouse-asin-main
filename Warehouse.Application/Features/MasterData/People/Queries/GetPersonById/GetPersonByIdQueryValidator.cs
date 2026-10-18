using FluentValidation;

namespace Warehouse.Application.Features.MasterData.People.Queries.GetPersonById;

internal sealed class GetPersonByIdQueryValidator : AbstractValidator<GetPersonByIdQuery>
{
    public GetPersonByIdQueryValidator() =>
        RuleFor(query => query.Id).NotEmpty();
}
