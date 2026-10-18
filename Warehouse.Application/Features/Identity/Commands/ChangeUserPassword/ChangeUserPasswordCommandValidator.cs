using FluentValidation;

namespace Warehouse.Application.Features.Identity.Commands.ChangeUserPassword;

internal sealed class ChangeUserPasswordCommandValidator
    : AbstractValidator<ChangeUserPasswordCommand>
{
    public ChangeUserPasswordCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();

        RuleFor(command => command.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);
    }
}
