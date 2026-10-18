using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.UpdateCurrentUserProfile;

public sealed record UpdateCurrentUserProfileCommand(
    Guid UserId,
    string FullName,
    string PhoneNumber,
    byte[]? ProfileImage,
    string? ProfileImageContentType,
    bool RemoveProfileImage) : IRequest<Result>;

internal sealed class UpdateCurrentUserProfileCommandHandler(IIdentityService identityService)
    : IRequestHandler<UpdateCurrentUserProfileCommand, Result>
{
    public async Task<Result> Handle(
        UpdateCurrentUserProfileCommand request,
        CancellationToken cancellationToken)
    {
        var result = await identityService.UpdateCurrentUserProfileAsync(
            new UpdateCurrentUserProfileRequest(
                request.UserId,
                request.FullName,
                request.PhoneNumber,
                request.ProfileImage,
                request.ProfileImageContentType,
                request.RemoveProfileImage),
            cancellationToken);

        return result.Succeeded
            ? Result.Success("Profile updated.")
            : Result.Error(result.Errors);
    }
}
