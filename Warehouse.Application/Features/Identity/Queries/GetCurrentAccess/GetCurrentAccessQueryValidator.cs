using FluentValidation;

namespace Warehouse.Application.Features.Identity.Queries.GetCurrentAccess;

internal sealed class GetCurrentAccessQueryValidator
    : AbstractValidator<GetCurrentAccessQuery>
{
    public GetCurrentAccessQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
    }
}
