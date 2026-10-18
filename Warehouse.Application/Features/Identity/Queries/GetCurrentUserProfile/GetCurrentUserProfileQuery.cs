using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;

namespace Warehouse.Application.Features.Identity.Queries.GetCurrentUserProfile;

public sealed record GetCurrentUserProfileQuery(Guid UserId)
    : IRequest<Result<GetCurrentUserProfileResponse>>;

internal sealed class GetCurrentUserProfileQueryHandler(IIdentityService identityService)
    : IRequestHandler<GetCurrentUserProfileQuery, Result<GetCurrentUserProfileResponse>>
{
    public async Task<Result<GetCurrentUserProfileResponse>> Handle(
        GetCurrentUserProfileQuery request,
        CancellationToken cancellationToken)
    {
        var result = await identityService.GetCurrentUserProfileAsync(
            request.UserId,
            cancellationToken);

        if (!result.Succeeded || result.Profile is null)
        {
            return Result<GetCurrentUserProfileResponse>.Error(result.Errors);
        }

        var profile = result.Profile;
        return Result<GetCurrentUserProfileResponse>.Success(
            new GetCurrentUserProfileResponse(
                profile.UserId,
                profile.UserName,
                profile.Email,
                profile.FullName,
                profile.PhoneNumber,
                profile.ProfileImageContentType,
                profile.ProfileImage));
    }
}
