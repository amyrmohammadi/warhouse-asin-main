namespace Warehouse.Infrastructure.Identity;

internal static class JwtOptionsValidator
{
    public static bool IsConfigured(JwtOptions options) =>
        !string.IsNullOrWhiteSpace(options.Issuer) &&
        !string.IsNullOrWhiteSpace(options.Audience) &&
        options.SecretKey.Length >= 32 &&
        options.AccessTokenExpirationMinutes is > 0 and <= 1440 &&
        options.RefreshTokenExpirationDays is > 0 and <= 365;

    public static void EnsureConfigured(JwtOptions options)
    {
        if (!IsConfigured(options))
        {
            throw new InvalidOperationException(
                "JWT is not configured. Set Jwt:Issuer, Jwt:Audience, a Jwt:SecretKey " +
                "with at least 32 characters, Jwt:AccessTokenExpirationMinutes " +
                "between 1 and 1440, and Jwt:RefreshTokenExpirationDays between 1 and 365.");
        }
    }
}
