using Warehouse.Application.Features.Identity.Commands.CreateUser;

namespace Warehouse.Application.Tests.Features.Identity.Commands.CreateUser;

public sealed class CreateUserFeatureTests
{
    [Fact]
    public async Task Send_WhenIdentityServiceSucceeds_ReturnsMappedUserId()
    {
        await using var context = new ApplicationTestContext();
        var userId = Guid.NewGuid();
        var command = new CreateUserCommand(
            "user@example.com",
            "Password1!",
            "Test User",
            "+989121234567",
            "test-user");
        context.IdentityService
            .CreateUserAsync(
                Arg.Is<CreateUserRequest>(request =>
                    request.Email == command.Email &&
                    request.Password == command.Password &&
                    request.FullName == command.FullName &&
                    request.PhoneNumber == command.PhoneNumber &&
                    request.UserName == command.UserName),
                Arg.Any<CancellationToken>())
            .Returns(new CreateUserResult(true, userId, []));

        var result = await context.Sender.Send(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(userId, result.Data.UserId);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public async Task Send_WhenIdentityServiceFails_ReturnsItsErrors()
    {
        await using var context = new ApplicationTestContext();
        var command = new CreateUserCommand(
            "user@example.com",
            "Password1!",
            "Test User",
            "+989121234567");
        string[] errors = ["Email already exists.", "User creation failed."];
        context.IdentityService
            .CreateUserAsync(Arg.Any<CreateUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateUserResult(false, null, errors));

        var result = await context.Sender.Send(command);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(errors, result.Messages);
    }

    [Fact]
    public async Task Send_WhenServiceSucceedsWithoutUserId_ReturnsError()
    {
        await using var context = new ApplicationTestContext();
        var command = new CreateUserCommand(
            "user@example.com",
            "Password1!",
            "Test User",
            "+989121234567");
        context.IdentityService
            .CreateUserAsync(Arg.Any<CreateUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateUserResult(true, null, []));

        var result = await context.Sender.Send(command);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(
            ["The user was created without a valid identifier."],
            result.Messages);
    }

    [Fact]
    public async Task Send_WhenCommandIsInvalid_ThrowsValidationException()
    {
        CreateUserCommand[] invalidCommands =
        [
            new("", "Password1!", "Test User", "+989121234567"),
            new("not-an-email", "Password1!", "Test User", "+989121234567"),
            new("user@example.com", "short", "Test User", "+989121234567"),
            new(new string('e', 250) + "@test.com", "Password1!", "Test User", "+989121234567"),
            new("user@example.com", new string('p', 129), "Test User", "+989121234567"),
            new("user@example.com", "Password1!", "", "+989121234567"),
            new("user@example.com", "Password1!", new string('n', 201), "+989121234567"),
            new("user@example.com", "Password1!", "Test User", ""),
            new("user@example.com", "Password1!", "Test User", "invalid-phone"),
            new("user@example.com", "Password1!", "Test User", "+989121234567", new string('u', 257))
        ];

        foreach (var command in invalidCommands)
        {
            await using var context = new ApplicationTestContext();

            await Assert.ThrowsAsync<ValidationException>(
                () => context.Sender.Send(command));

            await context.IdentityService.DidNotReceiveWithAnyArgs()
                .CreateUserAsync(default!, default);
        }
    }
}
