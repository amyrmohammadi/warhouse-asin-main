using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Warehouse.Application.Common.Interfaces;

namespace Warehouse.Server.Authorization;

public sealed record PermissionRequirement(string Permission)
    : IAuthorizationRequirement;

internal sealed class PermissionAuthorizationHandler(
    IUserAccessService userAccessService)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userIdValue = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return;
        }

        var access = await userAccessService.GetAccessAsync(userId);
        if (access.IsActive && access.Permissions.Contains(
                requirement.Permission,
                StringComparer.Ordinal))
        {
            context.Succeed(requirement);
        }
    }
}
