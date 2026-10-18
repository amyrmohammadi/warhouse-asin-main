using Warehouse.Application.Features.Identity.Commands.RefreshToken;

namespace Warehouse.Application.Tests.Features.Identity.Commands.RefreshToken;

public sealed class RefreshTokenFeatureTests
{
    [Fact]
    public async Task Send_WhenRefreshTokenIsValid_ReturnsMappedTokenResult()
    {
        await using var context = new ApplicationTestContext();
        var command = new RefreshTokenCommand("old-refresh-token");
        var serviceResult = IdentityTestData.ValidTokenResult();
        context.IdentityService
            .RefreshTokenAsync(
                Arg.Is<RefreshTokenRequest>(request =>
                    request.RefreshToken == command.RefreshToken),
                Arg.Any<CancellationToken>())
            .Returns(serviceResult);

        var result = await context.Sender.Send(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(serviceResult.UserId, result.Data.UserId);
        Assert.Equal(serviceResult.AccessToken, result.Data.AccessToken);
        Assert.Equal(serviceResult.RefreshToken, result.Data.RefreshToken);
        Assert.Equal(serviceResult.Permissions, result.Data.Permissions);
    }

    [Fact]
    public async Task Send_WhenIdentityServiceFails_ReturnsItsErrors()
    {
        await using var context = new ApplicationTestContext();
        var failedResult = IdentityTestData.ValidTokenResult() with
        {
            Succeeded = false,
            Errors = ["Refresh token is expired."]
        };
        context.IdentityService
            .RefreshTokenAsync(
                Arg.Any<RefreshTokenRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(failedResult);

        var result = await context.Sender.Send(
            new RefreshTokenCommand("expired-refresh-token"));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(["Refresh token is expired."], result.Messages);
    }

    [Fact]
    public async Task Send_WhenSuccessfulServiceResultHasIncompleteTokenData_ReturnsError()
    {
        foreach (var incompleteToken in IdentityTestData.IncompleteSuccessfulTokenResults())
        {
            await using var context = new ApplicationTestContext();
            context.IdentityService
                .RefreshTokenAsync(
                    Arg.Any<RefreshTokenRequest>(),
                    Arg.Any<CancellationToken>())
                .Returns(incompleteToken);

            var result = await context.Sender.Send(
                new RefreshTokenCommand("refresh-token"));

            Assert.False(result.IsSuccess);
            Assert.Null(result.Data);
            Assert.Equal(
                ["The authentication service returned incomplete token data."],
                result.Messages);
        }
    }

    [Fact]
    public async Task Send_WhenRefreshTokenIsEmpty_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(
            () => context.Sender.Send(new RefreshTokenCommand("")));

        await context.IdentityService.DidNotReceiveWithAnyArgs()
            .RefreshTokenAsync(default!, default);
    }
}
