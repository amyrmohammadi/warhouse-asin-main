using Warehouse.Application.Features.Identity.Queries.GetRolePolicies;

namespace Warehouse.Application.Tests.Features.Identity.Queries.GetRolePolicies;

public sealed class GetRolePoliciesFeatureTests
{
    [Fact]
    public async Task Send_WhenRoleExists_ReturnsGroupedPermissions()
    {
        await using var context = new ApplicationTestContext();
        context.IdentityService
            .GetRolePoliciesAsync("Admin", Arg.Any<CancellationToken>())
            .Returns(new GetRolePoliciesResult(
                true,
                new RolePoliciesSummary(
                    "Admin",
                    [
                        new RolePermissionGroupSummary(
                            "users",
                            "مدیریت کاربران",
                            [
                                new RolePermissionSummary(
                                    "UsersRead",
                                    "مشاهده کاربران",
                                    true)
                            ])
                    ]),
                []));

        var result = await context.Sender.Send(new GetRolePoliciesQuery("Admin"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("Admin", result.Data.RoleName);
        var group = Assert.Single(result.Data.Groups);
        var permission = Assert.Single(group.Permissions);
        Assert.True(permission.IsAssigned);
        Assert.Equal("UsersRead", permission.Name);
    }

    [Fact]
    public async Task Send_WhenRoleDoesNotExist_ReturnsServiceError()
    {
        await using var context = new ApplicationTestContext();
        context.IdentityService
            .GetRolePoliciesAsync("Missing", Arg.Any<CancellationToken>())
            .Returns(new GetRolePoliciesResult(false, null, ["Role not found."]));

        var result = await context.Sender.Send(new GetRolePoliciesQuery("Missing"));

        Assert.False(result.IsSuccess);
        Assert.Contains("Role not found.", result.Messages);
    }
}
