using FluentValidation;

namespace Warehouse.Application.Features.Identity.Commands.ChangeCurrentUserPassword;

internal sealed class ChangeCurrentUserPasswordCommandValidator
    : AbstractValidator<ChangeCurrentUserPasswordCommand>
{
    public ChangeCurrentUserPasswordCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();

        RuleFor(command => command.CurrentPassword)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(command => command.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .NotEqual(command => command.CurrentPassword)
            .WithMessage("The new password must be different from the current password.");
    }
}
