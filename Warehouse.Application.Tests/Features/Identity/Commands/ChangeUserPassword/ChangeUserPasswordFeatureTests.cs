using Warehouse.Application.Features.Identity.Commands.ChangeUserPassword;

namespace Warehouse.Application.Tests.Features.Identity.Commands.ChangeUserPassword;

public sealed class ChangeUserPasswordFeatureTests
{
    [Fact]
    public async Task Send_WithValidPassword_ChangesPassword()
    {
        await using var context = new ApplicationTestContext();
        var command = new ChangeUserPasswordCommand(Guid.NewGuid(), "NewPassword1!");
        context.IdentityService
            .ChangeUserPasswordAsync(
                Arg.Is<ChangeUserPasswordRequest>(request =>
                    request.UserId == command.UserId &&
                    request.NewPassword == command.NewPassword),
                Arg.Any<CancellationToken>())
            .Returns(new IdentityOperationResult(true, []));

        var result = await context.Sender.Send(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(["User password changed."], result.Messages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public async Task Send_WithInvalidPassword_ThrowsValidationException(string password)
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(new ChangeUserPasswordCommand(Guid.NewGuid(), password)));
    }
}
