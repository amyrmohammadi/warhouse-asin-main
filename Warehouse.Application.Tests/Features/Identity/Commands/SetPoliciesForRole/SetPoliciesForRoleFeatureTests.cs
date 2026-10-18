using Warehouse.Application.Common.Authorization;
using Warehouse.Application.Features.Identity.Commands.SetPoliciesForRole;

namespace Warehouse.Application.Tests.Features.Identity.Commands.SetPoliciesForRole;

public sealed class SetPoliciesForRoleFeatureTests
{
    [Fact]
    public async Task Send_WithSelectedPolicies_ReplacesRolePolicies()
    {
        await using var context = new ApplicationTestContext();
        string[] policies =
        [
            PermissionPolicyNames.UserManagement.UsersRead,
            PermissionPolicyNames.Auditing.AuditLogsRead
        ];
        context.IdentityService
            .SetPoliciesForRoleAsync(
                Arg.Is<SetPoliciesForRoleRequest>(request =>
                    request.RoleName == "Admin" &&
                    request.Policies.SequenceEqual(policies)),
                Arg.Any<CancellationToken>())
            .Returns(new IdentityOperationResult(true, []));

        var result = await context.Sender.Send(
            new SetPoliciesForRoleCommand("Admin", policies));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Send_WithEmptyList_AllowsRemovingAllPolicies()
    {
        await using var context = new ApplicationTestContext();
        context.IdentityService
            .SetPoliciesForRoleAsync(
                Arg.Is<SetPoliciesForRoleRequest>(request =>
                    request.RoleName == "User" && request.Policies.Count == 0),
                Arg.Any<CancellationToken>())
            .Returns(new IdentityOperationResult(true, []));

        var result = await context.Sender.Send(
            new SetPoliciesForRoleCommand("User", []));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Send_WithUnsupportedPolicy_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(
            new SetPoliciesForRoleCommand("Admin", ["UnsupportedPolicy"])));

        await context.IdentityService.DidNotReceiveWithAnyArgs()
            .SetPoliciesForRoleAsync(default!, default);
    }
}
