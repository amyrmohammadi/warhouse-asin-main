using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Features.Identity.Commands.Login;

namespace Warehouse.Application.Tests.Features.Identity.Commands.Login;

public sealed class LoginFeatureTests
{
    [Fact]
    public async Task Send_WhenCredentialsAreValid_ReturnsMappedTokensAndPermissions()
    {
        await using var context = new ApplicationTestContext();
        var command = new LoginCommand("user@example.com", "Password1!");
        var serviceResult = IdentityTestData.ValidTokenResult();
        context.IdentityService
            .LoginAsync(
                Arg.Is<LoginRequest>(request =>
                    request.Email == command.Email &&
                    request.Password == command.Password),
                Arg.Any<CancellationToken>())
            .Returns(serviceResult);

        var result = await context.Sender.Send(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(serviceResult.UserId, result.Data.UserId);
        Assert.Equal(serviceResult.AccessToken, result.Data.AccessToken);
        Assert.Equal(serviceResult.AccessTokenExpiresAtUtc, result.Data.AccessTokenExpiresAtUtc);
        Assert.Equal(serviceResult.RefreshToken, result.Data.RefreshToken);
        Assert.Equal(serviceResult.RefreshTokenExpiresAtUtc, result.Data.RefreshTokenExpiresAtUtc);
        Assert.Equal(
            [AppPermission.UsersRead, AppPermission.UserRolesManage],
            result.Data.Permissions);
    }

    [Fact]
    public async Task Send_WhenIdentityServiceFails_ReturnsItsErrors()
    {
        await using var context = new ApplicationTestContext();
        var command = new LoginCommand("user@example.com", "Password1!");
        var serviceResult = IdentityTestData.ValidTokenResult() with
        {
            Succeeded = false,
            Errors = ["Invalid credentials."]
        };
        context.IdentityService
            .LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns(serviceResult);

        var result = await context.Sender.Send(command);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(["Invalid credentials."], result.Messages);
    }

    [Fact]
    public async Task Send_WhenSuccessfulServiceResultHasIncompleteTokenData_ReturnsError()
    {
        foreach (var incompleteToken in IdentityTestData.IncompleteSuccessfulTokenResults())
        {
            await using var context = new ApplicationTestContext();
            context.IdentityService
                .LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
                .Returns(incompleteToken);

            var result = await context.Sender.Send(
                new LoginCommand("user@example.com", "Password1!"));

            Assert.False(result.IsSuccess);
            Assert.Null(result.Data);
            Assert.Equal(
                ["The authentication service returned incomplete token data."],
                result.Messages);
        }
    }

    [Fact]
    public async Task Send_WhenCommandIsInvalid_ThrowsValidationException()
    {
        LoginCommand[] invalidCommands =
        [
            new("", "Password1!"),
            new("   ", "Password1!"),
            new("user@example.com", ""),
            new(new string('u', 257), "Password1!"),
            new("user@example.com", new string('p', 129))
        ];

        foreach (var command in invalidCommands)
        {
            await using var context = new ApplicationTestContext();

            await Assert.ThrowsAsync<ValidationException>(
                () => context.Sender.Send(command));

            await context.IdentityService.DidNotReceiveWithAnyArgs()
                .LoginAsync(default!, default);
        }
    }
}
