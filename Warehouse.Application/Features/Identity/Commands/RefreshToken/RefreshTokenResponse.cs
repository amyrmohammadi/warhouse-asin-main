using Warehouse.Application.Common.Authorization;

namespace Warehouse.Application.Features.Identity.Commands.RefreshToken;

public sealed record RefreshTokenResponse(
    Guid UserId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    IReadOnlyCollection<AppPermission> Permissions);
