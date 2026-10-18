using AutoMapper;
using MediatR;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Identity;

namespace Warehouse.Application.Features.Identity.Commands.Login;

public sealed record LoginCommand(
    string Email,
    string Password) : IRequest<Result<LoginResponse>>;

internal sealed class LoginCommandHandler(
    IIdentityService identityService,
    IMapper mapper) : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var serviceRequest = mapper.Map<LoginRequest>(request);
        var result = await identityService.LoginAsync(serviceRequest, cancellationToken);

        if (!result.Succeeded)
        {
            return Result<LoginResponse>.Error(result.Errors);
        }

        if (!HasValidTokenData(result))
        {
            return Result<LoginResponse>.Error(
                "The authentication service returned incomplete token data.");
        }

        return Result<LoginResponse>.Success(mapper.Map<LoginResponse>(result));
    }

    private static bool HasValidTokenData(TokenResult result) =>
        result.UserId.HasValue &&
        !string.IsNullOrWhiteSpace(result.AccessToken) &&
        result.AccessTokenExpiresAtUtc.HasValue &&
        !string.IsNullOrWhiteSpace(result.RefreshToken) &&
        result.RefreshTokenExpiresAtUtc.HasValue;
}
