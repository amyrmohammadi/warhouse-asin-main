using Warehouse.Application.Features.Identity.Commands.SetUserActiveStatus;

namespace Warehouse.Application.Tests.Features.Identity.Commands.SetUserActiveStatus;

public sealed class SetUserActiveStatusFeatureTests
{
    [Fact]
    public async Task Send_ActivatesUser()
    {
        await using var context = new ApplicationTestContext();
        var command = new SetUserActiveStatusCommand(Guid.NewGuid(), true);
        context.IdentityService
            .SetUserActiveStatusAsync(
                Arg.Is<SetUserActiveStatusRequest>(request =>
                    request.UserId == command.UserId && request.IsActive),
                Arg.Any<CancellationToken>())
            .Returns(new IdentityOperationResult(true, []));

        var result = await context.Sender.Send(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(["User activated."], result.Messages);
    }

    [Fact]
    public async Task Send_WithEmptyUserId_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(new SetUserActiveStatusCommand(Guid.Empty, true)));
    }
}
