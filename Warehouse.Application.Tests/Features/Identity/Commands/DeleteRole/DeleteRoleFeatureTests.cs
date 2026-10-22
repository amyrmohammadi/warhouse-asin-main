using Warehouse.Application.Features.Identity.Commands.DeleteRole;

namespace Warehouse.Application.Tests.Features.Identity.Commands.DeleteRole;

public sealed class DeleteRoleFeatureTests
{
    [Fact]
    public async Task Send_WhenServiceSucceeds_ReturnsSuccess()
    {
        await using var context = new ApplicationTestContext();
        var roleId = Guid.NewGuid();
        context.IdentityService
            .DeleteRoleAsync(
                Arg.Is<DeleteRoleRequest>(request => request.RoleId == roleId),
                Arg.Any<CancellationToken>())
            .Returns(new IdentityOperationResult(true, []));

        var result = await context.Sender.Send(new DeleteRoleCommand(roleId));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Send_WithEmptyId_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(
            () => context.Sender.Send(new DeleteRoleCommand(Guid.Empty)));
    }
}
