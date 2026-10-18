using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Features.Identity.Queries.GetCurrentAccess;

namespace Warehouse.Application.Tests.Features.Identity.Queries.GetCurrentAccess;

public sealed class GetCurrentAccessFeatureTests
{
    [Fact]
    public async Task Send_WhenUserIsActive_ReturnsCurrentKnownPermissions()
    {
        await using var context = new ApplicationTestContext();
        var userId = Guid.NewGuid();
        context.UserAccessService
            .GetAccessAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new UserAccessSnapshot(
                true,
                ["UsersRead", "UsersRead", "UnknownPermission", "RolesRead"]));

        var result = await context.Sender.Send(new GetCurrentAccessQuery(userId));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(
            [AppPermission.UsersRead, AppPermission.RolesRead],
            result.Data.Permissions);
    }

    [Fact]
    public async Task Send_WhenUserIsInactive_ReturnsError()
    {
        await using var context = new ApplicationTestContext();
        var userId = Guid.NewGuid();
        context.UserAccessService
            .GetAccessAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new UserAccessSnapshot(false, []));

        var result = await context.Sender.Send(new GetCurrentAccessQuery(userId));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Send_WhenUserIdIsEmpty_ReturnsValidationError()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(new GetCurrentAccessQuery(Guid.Empty)));
    }
}
