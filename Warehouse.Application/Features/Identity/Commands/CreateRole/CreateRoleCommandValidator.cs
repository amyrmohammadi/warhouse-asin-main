using FluentValidation;

namespace Warehouse.Application.Features.Identity.Commands.CreateRole;

internal sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(256)
            .Matches(@"^[\p{L}\p{N}._\- ]+$")
            .WithMessage("Role name contains unsupported characters.");
    }
}
