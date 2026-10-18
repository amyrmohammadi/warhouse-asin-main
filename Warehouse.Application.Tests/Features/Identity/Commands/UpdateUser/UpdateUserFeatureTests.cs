using Warehouse.Application.Features.Identity.Commands.UpdateUser;

namespace Warehouse.Application.Tests.Features.Identity.Commands.UpdateUser;

public sealed class UpdateUserFeatureTests
{
    [Fact]
    public async Task Send_WithValidCommand_UpdatesUserInformation()
    {
        await using var context = new ApplicationTestContext();
        var command = new UpdateUserCommand(
            Guid.NewGuid(),
            "Amir Ahmadi",
            "+989121234567",
            "amir@example.com",
            "amir");
        context.IdentityService
            .UpdateUserAsync(
                Arg.Is<UpdateUserRequest>(request =>
                    request.UserId == command.UserId &&
                    request.FullName == command.FullName &&
                    request.PhoneNumber == command.PhoneNumber &&
                    request.Email == command.Email &&
                    request.UserName == command.UserName),
                Arg.Any<CancellationToken>())
            .Returns(new IdentityOperationResult(true, []));

        var result = await context.Sender.Send(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(["User information updated."], result.Messages);
    }

    [Fact]
    public async Task Send_WithInvalidCommand_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var command = new UpdateUserCommand(
            Guid.Empty,
            "",
            "invalid",
            "invalid",
            "");

        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(command));
        await context.IdentityService.DidNotReceiveWithAnyArgs()
            .UpdateUserAsync(default!, default);
    }
}
