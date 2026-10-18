using Warehouse.Application.Common.Authorization;

namespace Warehouse.Application.Features.Identity.Commands.Login;

public sealed record LoginResponse(
    Guid UserId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    IReadOnlyCollection<AppPermission> Permissions);
