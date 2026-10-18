using Warehouse.Application.Common.Authorization;

namespace Warehouse.Application.Tests.Common.Authorization;

public sealed class PermissionPolicyNamesTests
{
    [Fact]
    public void Groups_ContainEveryPermissionExactlyOnce()
    {
        var groupedPolicies = PermissionPolicyNames.Groups
            .SelectMany(group => group.Policies)
            .Select(policy => policy.Name)
            .ToArray();

        Assert.Equal(Enum.GetNames<AppPermission>().Order(), groupedPolicies.Order());
        Assert.Equal(groupedPolicies.Length, groupedPolicies.Distinct().Count());
        Assert.All(
            PermissionPolicyNames.Groups,
            group =>
            {
                Assert.False(string.IsNullOrWhiteSpace(group.Key));
                Assert.False(string.IsNullOrWhiteSpace(group.DisplayName));
                Assert.NotEmpty(group.Policies);
            });
    }
}
