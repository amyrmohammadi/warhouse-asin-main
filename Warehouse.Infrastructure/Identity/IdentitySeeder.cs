using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Interfaces;

namespace Warehouse.Infrastructure.Identity;

internal sealed class IdentitySeeder(
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<IdentitySeedOptions> options,
    TimeProvider timeProvider,
    ICurrentUserContext currentUserContext) : IIdentitySeeder
{
    private const string SeedAdminFullName = "مدیر سیستم";
    private const string SeedUserFullName = "کاربر سیستم";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        currentUserContext.SetSystem();
        var seedOptions = options.Value;
        EnsureConfigured(seedOptions);

        await EnsureRoleAsync(DefaultRoles.Admin, cancellationToken);
        await EnsureRoleAsync(DefaultRoles.User, cancellationToken);

        await EnsureUserAsync(
            seedOptions.AdminEmail,
            seedOptions.AdminPassword,
            SeedAdminFullName,
            DefaultRoles.Admin,
            cancellationToken);

        await EnsureUserAsync(
            seedOptions.UserEmail,
            seedOptions.UserPassword,
            SeedUserFullName,
            DefaultRoles.User,
            cancellationToken);

        await EnsureRolePermissionsAsync(
            DefaultRoles.Admin,
            PermissionPolicyNames.All,
            cancellationToken);
    }

    private async Task EnsureRoleAsync(
        string roleName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        ThrowIfFailed(result, $"Could not create the '{roleName}' role.");
    }

    private async Task EnsureUserAsync(
        string email,
        string password,
        string fullName,
        string roleName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedEmail = email.Trim();
        var user = await userManager.FindByEmailAsync(normalizedEmail);

        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                UserName = normalizedEmail,
                FullName = fullName,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAtUtc = timeProvider.GetUtcNow()
            };

            var createResult = await userManager.CreateAsync(user, password);
            ThrowIfFailed(createResult, $"Could not create the '{normalizedEmail}' user.");
        }
        else if (string.IsNullOrWhiteSpace(user.FullName))
        {
            user.FullName = fullName;
            var updateResult = await userManager.UpdateAsync(user);
            ThrowIfFailed(updateResult, $"Could not update the '{normalizedEmail}' user.");
        }

        if (await userManager.IsInRoleAsync(user, roleName))
        {
            return;
        }

        var roleResult = await userManager.AddToRoleAsync(user, roleName);
        ThrowIfFailed(roleResult, $"Could not add '{normalizedEmail}' to '{roleName}'.");

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        ThrowIfFailed(stampResult, $"Could not update the stamp for '{normalizedEmail}'.");
    }

    private async Task EnsureRolePermissionsAsync(
        string roleName,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByNameAsync(roleName)
            ?? throw new InvalidOperationException($"The '{roleName}' role was not found.");

        var existingClaims = await roleManager.GetClaimsAsync(role);
        var permissionsToAdd = permissions
            .Where(permission => !existingClaims.Any(claim =>
                claim.Type == ApplicationClaimTypes.Permission &&
                string.Equals(
                    claim.Value,
                    permission,
                    StringComparison.Ordinal)))
            .ToArray();

        if (permissionsToAdd.Length == 0)
        {
            return;
        }

        foreach (var permission in permissionsToAdd)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await roleManager.AddClaimAsync(
                role,
                new Claim(ApplicationClaimTypes.Permission, permission));
            ThrowIfFailed(result, $"Could not add '{permission}' to '{roleName}'.");
        }

        var users = await userManager.GetUsersInRoleAsync(roleName);
        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stampResult = await userManager.UpdateSecurityStampAsync(user);
            ThrowIfFailed(
                stampResult,
                $"Could not update the stamp for '{user.Email ?? user.Id.ToString()}'.");
        }
    }

    private static void EnsureConfigured(IdentitySeedOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AdminEmail) ||
            string.IsNullOrWhiteSpace(options.AdminPassword) ||
            string.IsNullOrWhiteSpace(options.UserEmail) ||
            string.IsNullOrWhiteSpace(options.UserPassword))
        {
            throw new InvalidOperationException(
                "IdentitySeed is not configured. AdminEmail, AdminPassword, " +
                "UserEmail and UserPassword are required.");
        }

        if (string.Equals(
                options.AdminEmail.Trim(),
                options.UserEmail.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "IdentitySeed admin and user email addresses must be different.");
        }
    }

    private static void ThrowIfFailed(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(" ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"{message} {errors}");
    }
}
