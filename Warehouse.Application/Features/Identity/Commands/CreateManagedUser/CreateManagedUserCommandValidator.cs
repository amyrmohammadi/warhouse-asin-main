using FluentValidation;

namespace Warehouse.Application.Features.Identity.Commands.CreateManagedUser;

internal sealed class CreateManagedUserCommandValidator
    : AbstractValidator<CreateManagedUserCommand>
{
    public CreateManagedUserCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);

        RuleFor(command => command.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.PhoneNumber)
            .NotEmpty()
            .MaximumLength(32)
            .Matches(@"^\+?[0-9۰-۹٠-٩][0-9۰-۹٠-٩\s()\-]{6,31}$")
            .WithMessage("Phone number format is invalid.");

        RuleFor(command => command.UserName)
            .MaximumLength(256)
            .When(command => !string.IsNullOrWhiteSpace(command.UserName));

        RuleFor(command => command.RoleNames)
            .NotNull()
            .Must(roleNames => roleNames.Count <= 100)
            .WithMessage("No more than 100 roles can be assigned at once.");
        RuleForEach(command => command.RoleNames)
            .NotEmpty()
            .MaximumLength(256);
    }
}
