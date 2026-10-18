using FluentValidation;

namespace Warehouse.Application.Features.Identity.Commands.SetRolesForUser;

internal sealed class SetRolesForUserCommandValidator
    : AbstractValidator<SetRolesForUserCommand>
{
    public SetRolesForUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.RoleNames)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(roleNames => roleNames.Count <= 100)
            .WithMessage("A user cannot be assigned more than 100 roles.");
        RuleForEach(command => command.RoleNames)
            .NotEmpty()
            .MaximumLength(256);
    }
}
