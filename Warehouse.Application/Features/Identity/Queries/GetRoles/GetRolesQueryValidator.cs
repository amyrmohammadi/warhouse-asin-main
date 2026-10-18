using FluentValidation;
using Warehouse.Application.Common.Validation;

namespace Warehouse.Application.Features.Identity.Queries.GetRoles;

internal sealed class GetRolesQueryValidator : AbstractValidator<GetRolesQuery>
{
    private static readonly string[] SupportedSortFields =
    [
        "name",
        "userCount",
        "permissionCount"
    ];

    public GetRolesQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Search)
            .MaximumLength(256)
            .When(query => !string.IsNullOrWhiteSpace(query.Search));
        RuleFor(query => query.RoleName)
            .MaximumLength(256)
            .When(query => !string.IsNullOrWhiteSpace(query.RoleName));
        RuleFor(query => query.SortBy)
            .Must(sortBy => SupportedSortFields.Contains(
                sortBy!,
                StringComparer.OrdinalIgnoreCase))
            .WithMessage("The selected role sort field is not supported.")
            .When(query => !string.IsNullOrWhiteSpace(query.SortBy));
        RuleFor(query => query.UserCount!)
            .SetValidator(new NumericFilterValidator<int>(minimum: 0))
            .When(query => query.UserCount is not null);
        RuleFor(query => query.PermissionCount!)
            .SetValidator(new NumericFilterValidator<int>(minimum: 0))
            .When(query => query.PermissionCount is not null);
    }
}
