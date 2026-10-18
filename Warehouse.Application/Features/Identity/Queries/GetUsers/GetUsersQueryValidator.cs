using FluentValidation;

namespace Warehouse.Application.Features.Identity.Queries.GetUsers;

internal sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    private static readonly string[] SupportedSortFields =
    [
        "userName",
        "email",
        "fullName",
        "phoneNumber",
        "isActive",
        "emailConfirmed",
        "createdAtUtc"
    ];

    public GetUsersQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Search)
            .MaximumLength(256)
            .When(query => !string.IsNullOrWhiteSpace(query.Search));
        RuleFor(query => query.FullName)
            .MaximumLength(200)
            .When(query => !string.IsNullOrWhiteSpace(query.FullName));
        RuleFor(query => query.UserName)
            .MaximumLength(256)
            .When(query => !string.IsNullOrWhiteSpace(query.UserName));
        RuleFor(query => query.Email)
            .MaximumLength(256)
            .When(query => !string.IsNullOrWhiteSpace(query.Email));
        RuleFor(query => query.RoleName)
            .MaximumLength(256)
            .When(query => !string.IsNullOrWhiteSpace(query.RoleName));
        RuleFor(query => query.SortBy)
            .Must(sortBy => SupportedSortFields.Contains(
                sortBy!,
                StringComparer.OrdinalIgnoreCase))
            .WithMessage("The selected user sort field is not supported.")
            .When(query => !string.IsNullOrWhiteSpace(query.SortBy));
    }
}
