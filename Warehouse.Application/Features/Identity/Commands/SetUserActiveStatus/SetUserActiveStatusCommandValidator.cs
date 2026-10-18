using FluentValidation;

namespace Warehouse.Application.Features.Identity.Commands.SetUserActiveStatus;

internal sealed class SetUserActiveStatusCommandValidator
    : AbstractValidator<SetUserActiveStatusCommand>
{
    public SetUserActiveStatusCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
    }
}
