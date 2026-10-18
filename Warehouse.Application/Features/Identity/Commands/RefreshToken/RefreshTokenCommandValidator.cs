using FluentValidation;

namespace Warehouse.Application.Features.Identity.Commands.RefreshToken;

internal sealed class RefreshTokenCommandValidator
    : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty();
    }
}
