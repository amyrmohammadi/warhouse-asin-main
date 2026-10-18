using Warehouse.Application.Features.Identity.Commands.ChangeCurrentUserPassword;
using Warehouse.Application.Features.Identity.Commands.UpdateCurrentUserProfile;
using Warehouse.Application.Features.Identity.Queries.GetCurrentUserProfile;

namespace Warehouse.Application.Tests.Features.Identity;

public sealed class CurrentUserProfileFeatureTests
{
    [Fact]
    public async Task GetProfile_WithValidUser_ReturnsProfile()
    {
        await using var context = new ApplicationTestContext();
        var userId = Guid.NewGuid();
        var profile = new CurrentUserProfile(
            userId,
            "amir",
            "amir@example.com",
            "Amir Ahmadi",
            "+989121234567",
            "image/png",
            [0x89, 0x50, 0x4E, 0x47]);
        context.IdentityService
            .GetCurrentUserProfileAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new GetCurrentUserProfileResult(true, profile, []));

        var result = await context.Sender.Send(new GetCurrentUserProfileQuery(userId));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(profile.FullName, result.Data.FullName);
        Assert.Equal(profile.ProfileImage, result.Data.ProfileImage);
    }

    [Fact]
    public async Task UpdateProfile_WithValidInput_UpdatesOnlyEditableFields()
    {
        await using var context = new ApplicationTestContext();
        var command = new UpdateCurrentUserProfileCommand(
            Guid.NewGuid(),
            "Amir Ahmadi",
            "+989121234567",
            [0xFF, 0xD8, 0xFF],
            "image/jpeg",
            false);
        context.IdentityService
            .UpdateCurrentUserProfileAsync(
                Arg.Is<UpdateCurrentUserProfileRequest>(request =>
                    request.UserId == command.UserId &&
                    request.FullName == command.FullName &&
                    request.PhoneNumber == command.PhoneNumber &&
                    request.ProfileImage == command.ProfileImage &&
                    request.ProfileImageContentType == command.ProfileImageContentType &&
                    !request.RemoveProfileImage),
                Arg.Any<CancellationToken>())
            .Returns(new IdentityOperationResult(true, []));

        var result = await context.Sender.Send(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Profile updated."], result.Messages);
    }

    [Fact]
    public async Task UpdateProfile_WithOversizedImage_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var command = new UpdateCurrentUserProfileCommand(
            Guid.NewGuid(),
            "Amir Ahmadi",
            "+989121234567",
            new byte[(2 * 1024 * 1024) + 1],
            "image/png",
            false);

        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(command));
        await context.IdentityService.DidNotReceiveWithAnyArgs()
            .UpdateCurrentUserProfileAsync(default!, default);
    }

    [Fact]
    public async Task ChangePassword_WithCurrentPassword_PassesBothPasswordsToIdentity()
    {
        await using var context = new ApplicationTestContext();
        var command = new ChangeCurrentUserPasswordCommand(
            Guid.NewGuid(),
            "CurrentPassword1!",
            "NewPassword2!");
        context.IdentityService
            .ChangeCurrentUserPasswordAsync(
                Arg.Is<ChangeCurrentUserPasswordRequest>(request =>
                    request.UserId == command.UserId &&
                    request.CurrentPassword == command.CurrentPassword &&
                    request.NewPassword == command.NewPassword),
                Arg.Any<CancellationToken>())
            .Returns(new IdentityOperationResult(true, []));

        var result = await context.Sender.Send(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Password changed."], result.Messages);
    }
}
