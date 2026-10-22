using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsReceipts;

public sealed class CreateGoodsReceiptValidationTests
{
    [Fact]
    public async Task Send_WithoutItems_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        await Assert.ThrowsAsync<ValidationException>(
            () => context.Sender.Send(CreateValidCommand() with { Items = [] }));
    }

    [Fact]
    public async Task Send_WithNewProductWithoutCodeAndUnit_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var invalid = CreateValidItem() with
        {
            ProductId = null, ProductTitle = "کالای جدید", ProductCode = null,
            UnitOfMeasureId = null
        };
        await Assert.ThrowsAsync<ValidationException>(
            () => context.Sender.Send(CreateValidCommand() with { Items = [invalid] }));
    }

    [Fact]
    public async Task Send_WithInvoiceAndWithoutInvoiceNumber_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var invalid = CreateValidItem() with
        {
            InvoiceStatus = InvoiceStatus.HasInvoice, InvoiceNumber = null
        };
        await Assert.ThrowsAsync<ValidationException>(
            () => context.Sender.Send(CreateValidCommand() with { Items = [invalid] }));
    }

    [Fact]
    public async Task Send_WithCompletelyIdenticalItemRows_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var item = CreateValidItem();

        await Assert.ThrowsAsync<ValidationException>(
            () => context.Sender.Send(CreateValidCommand() with { Items = [item, item] }));
    }

    [Fact]
    public async Task Send_WithNormalizedDuplicateNewProductTitles_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var first = CreateValidItem() with
        {
            ProductId = null, ProductTitle = "کالای-آزمایشی", ProductCode = "A-1",
            UnitOfMeasureId = Guid.NewGuid()
        };
        var second = CreateValidItem() with
        {
            ProductId = null, ProductTitle = "کالای آزمایشی", ProductCode = "B-2",
            UnitOfMeasureId = Guid.NewGuid()
        };
        await Assert.ThrowsAsync<ValidationException>(
            () => context.Sender.Send(CreateValidCommand() with { Items = [first, second] }));
    }

    [Fact]
    public async Task Send_WithNormalizedDuplicateNewProductCodes_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();
        var first = CreateValidItem() with
        {
            ProductId = null, ProductTitle = "کالای اول", ProductCode = "ab- 12",
            UnitOfMeasureId = Guid.NewGuid()
        };
        var second = CreateValidItem() with
        {
            ProductId = null, ProductTitle = "کالای دوم", ProductCode = "A B12",
            UnitOfMeasureId = Guid.NewGuid()
        };
        await Assert.ThrowsAsync<ValidationException>(
            () => context.Sender.Send(CreateValidCommand() with { Items = [first, second] }));
    }

    private static CreateGoodsReceiptCommand CreateValidCommand() => new(
        DateTimeOffset.UtcNow, null, Guid.NewGuid(), null, "12", [CreateValidItem()]);

    private static CreateGoodsReceiptItem CreateValidItem() => new(
        Guid.NewGuid(), null, null, null, Guid.NewGuid(), InvoiceStatus.DoesNotHave,
        null, null, null, null, 1, null, null);
}
