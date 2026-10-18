using Microsoft.AspNetCore.Authorization;
using Warehouse.Application.Common.Authorization;

namespace Warehouse.Server.Authorization;

public static class PermissionAuthorizationExtensions
{
    public static IServiceCollection AddPermissionAuthorization(
        this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            foreach (var permission in PermissionPolicyNames.All)
            {
                options.AddPolicy(
                    permission,
                    policy =>
                    {
                        policy.RequireAuthenticatedUser();
                        policy.AddRequirements(new PermissionRequirement(permission));
                    });
            }
        });

        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}
