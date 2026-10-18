using FluentValidation;
using Warehouse.Application.Common.Authorization;

namespace Warehouse.Application.Features.Identity.Commands.AddPoliciesToRole;

internal sealed class AddPoliciesToRoleCommandValidator
    : AbstractValidator<AddPoliciesToRoleCommand>
{
    public AddPoliciesToRoleCommandValidator()
    {
        RuleFor(command => command.RoleName)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(command => command.Policies).NotEmpty();
        RuleForEach(command => command.Policies)
            .NotEmpty()
            .MaximumLength(256)
            .Must(PermissionPolicyNames.IsDefined)
            .WithMessage("'{PropertyValue}' is not a supported policy.");
    }
}
