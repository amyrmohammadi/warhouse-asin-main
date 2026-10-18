using Warehouse.Application.Common.Authorization;

namespace Warehouse.Application.Tests.TestSupport;

internal static class IdentityTestData
{
    public static TokenResult ValidTokenResult()
    {
        var now = DateTimeOffset.UtcNow;

        return new TokenResult(
            true,
            Guid.NewGuid(),
            "access-token",
            now.AddMinutes(15),
            "refresh-token",
            now.AddDays(7),
            [AppPermission.UsersRead, AppPermission.UserRolesManage],
            []);
    }

    public static IEnumerable<TokenResult> IncompleteSuccessfulTokenResults()
    {
        var valid = ValidTokenResult();

        yield return valid with { UserId = null };
        yield return valid with { AccessToken = null };
        yield return valid with { AccessTokenExpiresAtUtc = null };
        yield return valid with { RefreshToken = null };
        yield return valid with { RefreshTokenExpiresAtUtc = null };
    }
}
