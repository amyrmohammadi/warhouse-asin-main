using FluentValidation;

namespace Warehouse.Application.Features.Identity.Commands.UpdateUser;

internal sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();

        RuleFor(command => command.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.PhoneNumber)
            .NotEmpty()
            .MaximumLength(32)
            .Matches(@"^\+?[0-9۰-۹٠-٩][0-9۰-۹٠-٩\s()\-]{6,31}$")
            .WithMessage("Phone number format is invalid.");

        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(command => command.UserName)
            .NotEmpty()
            .MaximumLength(256);
    }
}
