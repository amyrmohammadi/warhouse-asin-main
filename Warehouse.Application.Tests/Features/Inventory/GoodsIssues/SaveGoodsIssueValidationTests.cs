using Warehouse.Application.Features.Inventory.GoodsIssues.Commands.SaveGoodsIssue;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsIssues;

public sealed class SaveGoodsIssueValidationTests
{
    [Fact]
    public async Task Send_WithoutItems_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(ValidCommand() with { Items = [] }));
    }

    [Fact]
    public async Task Send_WithoutProductCode_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var item = ValidItem() with { ProductId = null, ProductCode = "" };
        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(ValidCommand() with { Items = [item] }));
    }

    [Fact]
    public async Task Send_WithoutDestination_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var item = ValidItem() with { DestinationId = null, DestinationTitle = null };
        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(ValidCommand() with { Items = [item] }));
    }

    [Fact]
    public async Task Send_WithNonPositiveQuantity_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var item = ValidItem() with { Quantity = 0 };
        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(ValidCommand() with { Items = [item] }));
    }

    [Fact]
    public async Task Send_WithReturnDateBeforeIssueDate_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var command = ValidCommand();
        var item = ValidItem() with { ReturnAtUtc = command.IssuedAtUtc.AddDays(-1) };
        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(command with { Items = [item] }));
    }

    private static SaveGoodsIssueCommand ValidCommand() => new(
        null, "OUT-100", DateTimeOffset.UtcNow, Guid.NewGuid(), null, [ValidItem()]);

    private static SaveGoodsIssueItem ValidItem() => new(
        Guid.NewGuid(), "P-100", 1, null, Guid.NewGuid(), null,
        "ارسال به پروژه", null, "واحد فناوری", null);
}
