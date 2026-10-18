using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsReceipts;

public sealed class CreateGoodsReceiptValidationTests
{
    [Fact]
    public async Task Send_WithoutItems_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var command = CreateValidCommand() with { Items = [] };

        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(command));
    }

    [Fact]
    public async Task Send_WithNewProductWithoutCodeAndUnit_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var command = CreateValidCommand() with
        {
            Items = [new CreateGoodsReceiptItem(null, "کالای جدید", null, null, 2, null, null)]
        };

        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(command));
    }

    [Fact]
    public async Task Send_WithInvoiceAndWithoutInvoiceNumber_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var command = CreateValidCommand() with
        {
            InvoiceStatus = InvoiceStatus.HasInvoice,
            InvoiceNumber = null
        };

        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(command));
    }

    [Fact]
    public async Task Send_WithNormalizedDuplicateNewProductTitles_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var command = CreateValidCommand() with
        {
            Items =
            [
                new CreateGoodsReceiptItem(null, "کالای-آزمایشی", "A-1", Guid.NewGuid(), 1, null, null),
                new CreateGoodsReceiptItem(null, "کالای آزمایشی", "B-2", Guid.NewGuid(), 1, null, null)
            ]
        };

        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(command));
    }

    [Fact]
    public async Task Send_WithNormalizedDuplicateNewProductCodes_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var command = CreateValidCommand() with
        {
            Items =
            [
                new CreateGoodsReceiptItem(null, "کالای اول", "ab- 12", Guid.NewGuid(), 1, null, null),
                new CreateGoodsReceiptItem(null, "کالای دوم", "A B12", Guid.NewGuid(), 1, null, null)
            ]
        };

        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(command));
    }

    private static CreateGoodsReceiptCommand CreateValidCommand() => new(
        DateTimeOffset.UtcNow,
        null,
        Guid.NewGuid(),
        null,
        Guid.NewGuid(),
        InvoiceStatus.DoesNotHave,
        null,
        null,
        null,
        null,
        "12",
        [new CreateGoodsReceiptItem(Guid.NewGuid(), null, null, null, 1, null, null)]);
}
