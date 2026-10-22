using FluentValidation;

namespace Warehouse.Application.Features.Identity.Commands.UpdateRole;

internal sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(command => command.RoleId).NotEmpty();
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(256)
            .Matches(@"^[\p{L}\p{N}._\- ]+$")
            .WithMessage("Role name contains unsupported characters.");
    }
}
