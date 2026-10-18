using FluentValidation;

namespace Warehouse.Application.Features.Identity.Queries.GetCurrentUserProfile;

internal sealed class GetCurrentUserProfileQueryValidator
    : AbstractValidator<GetCurrentUserProfileQuery>
{
    public GetCurrentUserProfileQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
    }
}
