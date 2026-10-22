using Warehouse.Application.Features.Identity.Commands.UpdateRole;

namespace Warehouse.Application.Tests.Features.Identity.Commands.UpdateRole;

public sealed class UpdateRoleFeatureTests
{
    [Fact]
    public async Task Send_WhenServiceSucceeds_ReturnsSuccess()
    {
        await using var context = new ApplicationTestContext();
        var roleId = Guid.NewGuid();
        context.IdentityService
            .UpdateRoleAsync(
                Arg.Is<UpdateRoleRequest>(request =>
                    request.RoleId == roleId && request.Name == "WarehouseManager"),
                Arg.Any<CancellationToken>())
            .Returns(new IdentityOperationResult(true, []));

        var result = await context.Sender.Send(
            new UpdateRoleCommand(roleId, "WarehouseManager"));

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Invalid/Role")]
    public async Task Send_WithInvalidName_ThrowsValidationException(string roleName)
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(
            () => context.Sender.Send(new UpdateRoleCommand(Guid.NewGuid(), roleName)));
    }
}
