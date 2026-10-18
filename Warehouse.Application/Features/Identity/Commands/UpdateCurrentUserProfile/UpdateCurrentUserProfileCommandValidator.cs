using FluentValidation;

namespace Warehouse.Application.Features.Identity.Commands.UpdateCurrentUserProfile;

internal sealed class UpdateCurrentUserProfileCommandValidator
    : AbstractValidator<UpdateCurrentUserProfileCommand>
{
    private const int MaximumImageSize = 2 * 1024 * 1024;
    private static readonly string[] SupportedImageContentTypes =
        ["image/jpeg", "image/png", "image/webp"];

    public UpdateCurrentUserProfileCommandValidator()
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

        RuleFor(command => command.ProfileImage)
            .Must(image => image is null || image.Length <= MaximumImageSize)
            .WithMessage("Profile image cannot be larger than 2 MB.");

        RuleFor(command => command.ProfileImageContentType)
            .Must(contentType =>
                contentType is not null && SupportedImageContentTypes.Contains(contentType))
            .When(command => command.ProfileImage is not null)
            .WithMessage("Only JPEG, PNG, and WebP profile images are supported.");

        RuleFor(command => command)
            .Must(command => !(command.RemoveProfileImage && command.ProfileImage is not null))
            .WithMessage("A new profile image cannot be uploaded while removing the current image.");
    }
}
