using FluentValidation;

namespace Warehouse.Application.Features.Identity.Queries.GetRolePolicies;

internal sealed class GetRolePoliciesQueryValidator
    : AbstractValidator<GetRolePoliciesQuery>
{
    public GetRolePoliciesQueryValidator()
    {
        RuleFor(query => query.RoleName)
            .NotEmpty()
            .MaximumLength(256);
    }
}
