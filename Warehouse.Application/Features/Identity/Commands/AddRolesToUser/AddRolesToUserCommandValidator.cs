using FluentValidation;

namespace Warehouse.Application.Features.Identity.Commands.AddRolesToUser;

internal sealed class AddRolesToUserCommandValidator
    : AbstractValidator<AddRolesToUserCommand>
{
    public AddRolesToUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.RoleNames).NotEmpty();
        RuleForEach(command => command.RoleNames)
            .NotEmpty()
            .MaximumLength(256);
    }
}
