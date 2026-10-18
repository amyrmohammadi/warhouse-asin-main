using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure.Identity;

internal sealed class UserAccessService(
    ApplicationDbContext dbContext) : IUserAccessService
{
    private Guid? _cachedUserId;
    private UserAccessSnapshot? _cachedSnapshot;

    public async Task<UserAccessSnapshot> GetAccessAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (_cachedUserId == userId && _cachedSnapshot is not null)
        {
            return _cachedSnapshot;
        }

        var isActive = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => (bool?)user.IsActive)
            .SingleOrDefaultAsync(cancellationToken) ?? false;

        if (!isActive)
        {
            return Cache(userId, new UserAccessSnapshot(false, []));
        }

        var permissions = await (
                from userRole in dbContext.UserRoles.AsNoTracking()
                join roleClaim in dbContext.RoleClaims.AsNoTracking()
                    on userRole.RoleId equals roleClaim.RoleId
                where userRole.UserId == userId &&
                      roleClaim.ClaimType == ApplicationClaimTypes.Permission &&
                      roleClaim.ClaimValue != null
                select roleClaim.ClaimValue!)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return Cache(
            userId,
            new UserAccessSnapshot(true, permissions));
    }

    private UserAccessSnapshot Cache(Guid userId, UserAccessSnapshot snapshot)
    {
        _cachedUserId = userId;
        _cachedSnapshot = snapshot;
        return snapshot;
    }
}
