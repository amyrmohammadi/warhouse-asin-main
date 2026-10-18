using Warehouse.Application.Features.Identity.Commands.CreateRole;

namespace Warehouse.Application.Tests.Features.Identity.Commands.CreateRole;

public sealed class CreateRoleFeatureTests
{
    [Fact]
    public async Task Send_WhenServiceSucceeds_ReturnsSuccess()
    {
        await using var context = new ApplicationTestContext();
        context.IdentityService
            .CreateRoleAsync(
                Arg.Is<CreateRoleRequest>(request => request.Name == "WarehouseManager"),
                Arg.Any<CancellationToken>())
            .Returns(new IdentityOperationResult(true, []));

        var result = await context.Sender.Send(
            new CreateRoleCommand("WarehouseManager"));

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Invalid/Role")]
    public async Task Send_WithInvalidName_ThrowsValidationException(string roleName)
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(
            () => context.Sender.Send(new CreateRoleCommand(roleName)));

        await context.IdentityService.DidNotReceiveWithAnyArgs()
            .CreateRoleAsync(default!, default);
    }
}
