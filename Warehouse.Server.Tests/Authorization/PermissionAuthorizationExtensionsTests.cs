using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Warehouse.Application.Common.Authorization;
using Warehouse.Server.Authorization;

namespace Warehouse.Server.Tests.Authorization;

public sealed class PermissionAuthorizationExtensionsTests
{
    public static IEnumerable<object[]> PermissionNames =>
        PermissionPolicyNames.All.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(PermissionNames))]
    public async Task AddPermissionAuthorization_RegistersDynamicPolicyForEveryPermission(
        string permission)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPermissionAuthorization();
        await using var serviceProvider = services.BuildServiceProvider();
        var policyProvider = serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await policyProvider.GetPolicyAsync(permission);

        Assert.NotNull(policy);
        Assert.Contains(
            policy.Requirements,
            requirement => requirement is DenyAnonymousAuthorizationRequirement);
        var permissionRequirement = Assert.Single(
            policy.Requirements.OfType<PermissionRequirement>());
        Assert.Equal(permission, permissionRequirement.Permission);
    }
}
