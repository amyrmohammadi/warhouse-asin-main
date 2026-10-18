using AutoMapper;
using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken)
    : IRequest<Result<RefreshTokenResponse>>;

internal sealed class RefreshTokenCommandHandler(
    IIdentityService identityService,
    IMapper mapper) : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
{
    public async Task<Result<RefreshTokenResponse>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        var serviceRequest = mapper.Map<RefreshTokenRequest>(request);
        var result = await identityService.RefreshTokenAsync(serviceRequest, cancellationToken);

        if (!result.Succeeded)
        {
            return Result<RefreshTokenResponse>.Error(result.Errors);
        }

        if (!HasValidTokenData(result))
        {
            return Result<RefreshTokenResponse>.Error(
                "The authentication service returned incomplete token data.");
        }

        return Result<RefreshTokenResponse>.Success(
            mapper.Map<RefreshTokenResponse>(result));
    }

    private static bool HasValidTokenData(TokenResult result) =>
        result.UserId.HasValue &&
        !string.IsNullOrWhiteSpace(result.AccessToken) &&
        result.AccessTokenExpiresAtUtc.HasValue &&
        !string.IsNullOrWhiteSpace(result.RefreshToken) &&
        result.RefreshTokenExpiresAtUtc.HasValue;
}
