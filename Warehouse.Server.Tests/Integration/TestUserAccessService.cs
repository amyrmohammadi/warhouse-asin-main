using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Interfaces;

namespace Warehouse.Server.Tests.Integration;

internal sealed class TestUserAccessService(
    IHttpContextAccessor httpContextAccessor) : IUserAccessService
{
    public Task<UserAccessSnapshot> GetAccessAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var permissions = principal?
            .FindAll(ApplicationClaimTypes.Permission)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        var isActive = principal?.Identity?.IsAuthenticated == true &&
            principal.FindFirstValue(ClaimTypes.NameIdentifier) == userId.ToString();

        return Task.FromResult(new UserAccessSnapshot(isActive, permissions));
    }
}
