using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Features.Identity.Commands.AddPoliciesToRole;

namespace Warehouse.Application.Tests.Features.Identity.Commands.AddPoliciesToRole;

public sealed class AddPoliciesToRoleFeatureTests
{
    [Fact]
    public async Task Send_WhenServiceSucceeds_MapsRequestAndReturnsSuccess()
    {
        await using var context = new ApplicationTestContext();
        var command = new AddPoliciesToRoleCommand(
            "Admin",
            [PermissionPolicyNames.UserManagement.UsersRead, PermissionPolicyNames.RoleManagement.RolePoliciesManage]);
        context.IdentityService
            .AddPoliciesToRoleAsync(
                Arg.Is<AddPoliciesToRoleRequest>(request =>
                    request.RoleName == command.RoleName &&
                    request.Policies.SequenceEqual(command.Policies)),
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
        string[] errors = ["Role not found."];
        context.IdentityService
            .AddPoliciesToRoleAsync(
                Arg.Any<AddPoliciesToRoleRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(new IdentityOperationResult(false, errors));

        var result = await context.Sender.Send(
            new AddPoliciesToRoleCommand("MissingRole", [PermissionPolicyNames.UserManagement.UsersRead]));

        Assert.False(result.IsSuccess);
        Assert.Equal(errors, result.Messages);
    }

    [Fact]
    public async Task Send_WhenCommandIsInvalid_ThrowsValidationException()
    {
        AddPoliciesToRoleCommand[] invalidCommands =
        [
            new("", [PermissionPolicyNames.UserManagement.UsersRead]),
            new(new string('r', 257), [PermissionPolicyNames.UserManagement.UsersRead]),
            new("Admin", []),
            new("Admin", [""]),
            new("Admin", [new string('p', 257)]),
            new("Admin", ["UnsupportedPolicy"])
        ];

        foreach (var command in invalidCommands)
        {
            await using var context = new ApplicationTestContext();

            await Assert.ThrowsAsync<ValidationException>(
                () => context.Sender.Send(command));

            await context.IdentityService.DidNotReceiveWithAnyArgs()
                .AddPoliciesToRoleAsync(default!, default);
        }
    }
}
