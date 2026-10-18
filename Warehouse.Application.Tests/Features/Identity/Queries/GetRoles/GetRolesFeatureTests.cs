using Warehouse.Application.Features.Identity.Queries.GetRoles;

namespace Warehouse.Application.Tests.Features.Identity.Queries.GetRoles;

public sealed class GetRolesFeatureTests
{
    [Fact]
    public async Task Send_ReturnsMappedRoles()
    {
        await using var context = new ApplicationTestContext();
        var roleId = Guid.NewGuid();
        context.IdentityService
            .GetRolesAsync(
                Arg.Any<GetRolesRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new GetRolesResult(
                [
                    new RoleSummary(
                        roleId,
                        "Admin",
                        2,
                        4)
                ],
                1,
                1,
                1,
                20,
                false));

        var result = await context.Sender.Send(new GetRolesQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        var role = Assert.Single(result.Data.Items);
        Assert.Equal(roleId, role.Id);
        Assert.Equal("Admin", role.Name);
        Assert.Equal(2, role.UserCount);
        Assert.Equal(4, role.PermissionCount);
        Assert.Equal(1, result.Data.TotalCount);
        Assert.False(result.Data.PaginationEnabled);
    }
}
