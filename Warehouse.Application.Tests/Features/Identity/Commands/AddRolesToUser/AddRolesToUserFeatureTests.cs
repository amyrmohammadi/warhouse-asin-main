using Warehouse.Application.Features.Identity.Commands.AddRolesToUser;

namespace Warehouse.Application.Tests.Features.Identity.Commands.AddRolesToUser;

public sealed class AddRolesToUserFeatureTests
{
    [Fact]
    public async Task Send_WhenServiceSucceeds_MapsRequestAndReturnsSuccess()
    {
        await using var context = new ApplicationTestContext();
        var userId = Guid.NewGuid();
        var command = new AddRolesToUserCommand(userId, ["Admin", "User"]);
        context.IdentityService
            .AddRolesToUserAsync(
                Arg.Is<AddRolesToUserRequest>(request =>
                    request.UserId == userId &&
                    request.RoleNames.SequenceEqual(command.RoleNames)),
                Arg.Any<CancellationToken>())
            .Returns(new IdentityOperationResult(true, []));

        var result = await context.Sender.Send(command);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public async Task Send_WhenServiceFails_ReturnsItsErrors()
    {
        await using var context = new ApplicationTestContext();
        string[] errors = ["User not found.", "Role not found."];
        context.IdentityService
            .AddRolesToUserAsync(
                Arg.Any<AddRolesToUserRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new IdentityOperationResult(false, errors));

        var result = await context.Sender.Send(
            new AddRolesToUserCommand(Guid.NewGuid(), ["Admin"]));

        Assert.False(result.IsSuccess);
        Assert.Equal(errors, result.Messages);
    }

    [Fact]
    public async Task Send_WhenCommandIsInvalid_ThrowsValidationException()
    {
        AddRolesToUserCommand[] invalidCommands =
        [
            new(Guid.Empty, ["Admin"]),
            new(Guid.NewGuid(), []),
            new(Guid.NewGuid(), [""]),
            new(Guid.NewGuid(), [new string('r', 257)])
        ];

        foreach (var command in invalidCommands)
        {
            await using var context = new ApplicationTestContext();

            await Assert.ThrowsAsync<ValidationException>(
                () => context.Sender.Send(command));

            await context.IdentityService.DidNotReceiveWithAnyArgs()
                .AddRolesToUserAsync(default!, default);
        }
    }
}
