using Warehouse.Application.Features.Identity.Commands.CreateManagedUser;

namespace Warehouse.Application.Tests.Features.Identity.Commands.CreateManagedUser;

public sealed class CreateManagedUserFeatureTests
{
    [Fact]
    public async Task Send_MapsStatusAndRolesAndReturnsUserId()
    {
        await using var context = new ApplicationTestContext();
        var userId = Guid.NewGuid();
        var command = new CreateManagedUserCommand(
            "managed@example.com",
            "Password1!",
            "Managed User",
            "+989121234567",
            "managed-user",
            true,
            ["Admin", "User"]);
        context.IdentityService
            .CreateManagedUserAsync(
                Arg.Is<CreateManagedUserRequest>(request =>
                    request.Email == command.Email &&
                    request.Password == command.Password &&
                    request.FullName == command.FullName &&
                    request.PhoneNumber == command.PhoneNumber &&
                    request.UserName == command.UserName &&
                    request.IsActive &&
                    request.RoleNames.SequenceEqual(command.RoleNames)),
                Arg.Any<CancellationToken>())
            .Returns(new CreateUserResult(true, userId, []));

        var result = await context.Sender.Send(command);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(userId, result.Data.UserId);
    }

    [Fact]
    public async Task Send_WithInvalidInput_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(
            new CreateManagedUserCommand(
                "invalid-email",
                "short",
                "",
                "invalid-phone",
                null,
                false,
                [])));

        await context.IdentityService.DidNotReceiveWithAnyArgs()
            .CreateManagedUserAsync(default!, default);
    }
}
