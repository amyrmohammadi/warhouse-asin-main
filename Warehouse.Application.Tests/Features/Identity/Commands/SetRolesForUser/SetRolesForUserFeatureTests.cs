using Warehouse.Application.Features.Identity.Commands.SetRolesForUser;

namespace Warehouse.Application.Tests.Features.Identity.Commands.SetRolesForUser;

public sealed class SetRolesForUserFeatureTests
{
    [Fact]
    public async Task Send_WhenServiceSucceeds_MapsRequestAndReturnsSuccess()
    {
        await using var context = new ApplicationTestContext();
        var userId = Guid.NewGuid();
        var command = new SetRolesForUserCommand(userId, ["Admin", "User"]);
        context.IdentityService
            .SetRolesForUserAsync(
                Arg.Is<SetRolesForUserRequest>(request =>
                    request.UserId == userId &&
                    request.RoleNames.SequenceEqual(command.RoleNames)),
                Arg.Any<CancellationToken>())
            .Returns(new IdentityOperationResult(true, []));

        var result = await context.Sender.Send(command);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public async Task Send_WithEmptyRoleList_AllowsRemovingAllRoles()
    {
        await using var context = new ApplicationTestContext();
        var userId = Guid.NewGuid();
        context.IdentityService
            .SetRolesForUserAsync(
                Arg.Is<SetRolesForUserRequest>(request =>
                    request.UserId == userId &&
                    request.RoleNames.Count == 0),
                Arg.Any<CancellationToken>())
            .Returns(new IdentityOperationResult(true, []));

        var result = await context.Sender.Send(
            new SetRolesForUserCommand(userId, []));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Send_WhenServiceFails_ReturnsItsErrors()
    {
        await using var context = new ApplicationTestContext();
        string[] errors = ["User not found."];
        context.IdentityService
            .SetRolesForUserAsync(
                Arg.Any<SetRolesForUserRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new IdentityOperationResult(false, errors));

        var result = await context.Sender.Send(
            new SetRolesForUserCommand(Guid.NewGuid(), ["Admin"]));

        Assert.False(result.IsSuccess);
        Assert.Equal(errors, result.Messages);
    }

    [Fact]
    public async Task Send_WhenCommandIsInvalid_ThrowsValidationException()
    {
        SetRolesForUserCommand[] invalidCommands =
        [
            new(Guid.Empty, ["Admin"]),
            new(Guid.NewGuid(), null!),
            new(Guid.NewGuid(), [""]),
            new(Guid.NewGuid(), [new string('r', 257)]),
            new(Guid.NewGuid(), Enumerable.Repeat("Role", 101).ToArray())
        ];

        foreach (var command in invalidCommands)
        {
            await using var context = new ApplicationTestContext();

            await Assert.ThrowsAsync<ValidationException>(
                () => context.Sender.Send(command));

            await context.IdentityService.DidNotReceiveWithAnyArgs()
                .SetRolesForUserAsync(default!, default);
        }
    }
}
