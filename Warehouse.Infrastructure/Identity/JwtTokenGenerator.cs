using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure.Identity;

internal sealed record JwtToken(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    IReadOnlyCollection<AppPermission> Permissions);

internal interface IJwtTokenGenerator
{
    Task<JwtToken> GenerateAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default);

    Task<JwtToken?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}

internal sealed class JwtTokenGenerator(
    IOptions<JwtOptions> options,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    ApplicationDbContext dbContext,
    TimeProvider timeProvider,
    ICurrentUserContext currentUserContext) : IJwtTokenGenerator
{
    private const int RefreshTokenSizeInBytes = 64;

    public async Task<JwtToken> GenerateAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        currentUserContext.SetCurrentUser(
            user.Id,
            user.FullName);

        var jwtOptions = options.Value;
        JwtOptionsValidator.EnsureConfigured(jwtOptions);

        var now = timeProvider.GetUtcNow();
        var securityStamp = await userManager.GetSecurityStampAsync(user);
        var generatedRefreshToken = CreateRefreshToken(
            user.Id,
            securityStamp,
            user.ConcurrencyStamp,
            now,
            jwtOptions.RefreshTokenExpirationDays);

        var result = await CreateResultAsync(
            user,
            generatedRefreshToken.RawToken,
            generatedRefreshToken.Entity.ExpiresAtUtc,
            now,
            jwtOptions,
            cancellationToken);

        dbContext.RefreshTokens.Add(generatedRefreshToken.Entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    public async Task<JwtToken?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var jwtOptions = options.Value;
        JwtOptionsValidator.EnsureConfigured(jwtOptions);

        var now = timeProvider.GetUtcNow();
        var tokenHash = HashRefreshToken(refreshToken);
        var storedToken = await dbContext.RefreshTokens
            .AsNoTracking()
            .Include(token => token.User)
            .SingleOrDefaultAsync(
                token => token.TokenHash == tokenHash,
                cancellationToken);

        if (storedToken is null)
        {
            return null;
        }

        currentUserContext.SetCurrentUser(
            storedToken.User.Id,
            storedToken.User.FullName);

        if (storedToken.RevokedAtUtc is not null)
        {
            // Reuse of a rotated token is treated as a compromise.
            await RevokeAllUserTokensAsync(storedToken.UserId, now, cancellationToken);
            return null;
        }

        if (storedToken.ExpiresAtUtc <= now)
        {
            return null;
        }

        if (!storedToken.User.IsActive)
        {
            await RevokeAllUserTokensAsync(storedToken.UserId, now, cancellationToken);
            return null;
        }

        var currentSecurityStamp = await userManager.GetSecurityStampAsync(storedToken.User);
        if (!string.Equals(
                currentSecurityStamp,
                storedToken.SecurityStamp,
                StringComparison.Ordinal) ||
            !string.Equals(
                storedToken.User.ConcurrencyStamp,
                storedToken.ConcurrencyStamp,
                StringComparison.Ordinal))
        {
            await RevokeAllUserTokensAsync(storedToken.UserId, now, cancellationToken);
            return null;
        }

        var replacementToken = CreateRefreshToken(
            storedToken.UserId,
            currentSecurityStamp,
            storedToken.User.ConcurrencyStamp,
            now,
            jwtOptions.RefreshTokenExpirationDays);

        var result = await CreateResultAsync(
            storedToken.User,
            replacementToken.RawToken,
            replacementToken.Entity.ExpiresAtUtc,
            now,
            jwtOptions,
            cancellationToken);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var updatedRows = await dbContext.RefreshTokens
            .Where(token =>
                token.Id == storedToken.Id &&
                token.RevokedAtUtc == null &&
                token.ExpiresAtUtc > now &&
                token.SecurityStamp == currentSecurityStamp &&
                token.ConcurrencyStamp == storedToken.User.ConcurrencyStamp)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAtUtc, now)
                    .SetProperty(
                        token => token.ReplacedByTokenHash,
                        replacementToken.Entity.TokenHash),
                cancellationToken);

        if (updatedRows != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        dbContext.RefreshTokens.Add(replacementToken.Entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    private async Task<JwtToken> CreateResultAsync(
        ApplicationUser user,
        string refreshToken,
        DateTimeOffset refreshTokenExpiresAtUtc,
        DateTimeOffset issuedAt,
        JwtOptions jwtOptions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var accessTokenExpiresAt = issuedAt.AddMinutes(
            jwtOptions.AccessTokenExpirationMinutes);
        var claims = await GetClaimsAsync(user, cancellationToken);
        var credentials = new SigningCredentials(
            JwtSecurityKeyFactory.Create(jwtOptions.SecretKey),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: accessTokenExpiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new JwtToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            accessTokenExpiresAt,
            refreshToken,
            refreshTokenExpiresAtUtc,
            GetPermissions(claims));
    }

    private async Task<IReadOnlyCollection<Claim>> GetClaimsAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName ?? string.Empty)
        };

        claims.AddRange(await userManager.GetClaimsAsync(user));

        foreach (var roleName in roles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            claims.Add(new Claim(ClaimTypes.Role, roleName));

            var role = await roleManager.FindByNameAsync(roleName);
            if (role is not null)
            {
                claims.AddRange(await roleManager.GetClaimsAsync(role));
            }
        }

        return claims
            .GroupBy(claim => new { claim.Type, claim.Value })
            .Select(group => group.First())
            .ToArray();
    }

    private static IReadOnlyCollection<AppPermission> GetPermissions(
        IEnumerable<Claim> claims) =>
        claims
            .Where(claim => claim.Type == ApplicationClaimTypes.Permission)
            .Select(claim => Enum.TryParse<AppPermission>(
                claim.Value,
                ignoreCase: false,
                out var permission)
                ? permission
                : (AppPermission?)null)
            .Where(permission => permission.HasValue)
            .Select(permission => permission!.Value)
            .Distinct()
            .ToArray();

    private static GeneratedRefreshToken CreateRefreshToken(
        Guid userId,
        string? securityStamp,
        string? concurrencyStamp,
        DateTimeOffset createdAtUtc,
        int expirationDays)
    {
        var rawToken = Base64UrlEncoder.Encode(
            RandomNumberGenerator.GetBytes(RefreshTokenSizeInBytes));

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashRefreshToken(rawToken),
            SecurityStamp = securityStamp ?? string.Empty,
            ConcurrencyStamp = concurrencyStamp ?? string.Empty,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = createdAtUtc.AddDays(expirationDays)
        };

        return new GeneratedRefreshToken(rawToken, entity);
    }

    private async Task RevokeAllUserTokensAsync(
        Guid userId,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken)
    {
        await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    token => token.RevokedAtUtc,
                    revokedAtUtc),
                cancellationToken);
    }

    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private sealed record GeneratedRefreshToken(
        string RawToken,
        RefreshToken Entity);
}
