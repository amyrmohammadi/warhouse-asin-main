using FluentValidation;
using Warehouse.Application.Common.Authorization;

namespace Warehouse.Application.Features.Identity.Commands.SetPoliciesForRole;

internal sealed class SetPoliciesForRoleCommandValidator
    : AbstractValidator<SetPoliciesForRoleCommand>
{
    public SetPoliciesForRoleCommandValidator()
    {
        RuleFor(command => command.RoleName)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(command => command.Policies).NotNull();
        RuleForEach(command => command.Policies)
            .NotEmpty()
            .MaximumLength(256)
            .Must(PermissionPolicyNames.IsDefined)
            .WithMessage("'{PropertyValue}' is not a supported policy.");
    }
}
