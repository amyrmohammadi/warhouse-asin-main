using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.Invoices.Commands.UpdateInvoiceItem;
using Warehouse.Application.Tests.TestSupport;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.Invoices;

public sealed class UpdateInvoiceItemTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("REC-OLD")]
    public async Task Update_AddsOrChangesReceiptNumberAndAdvancesConcurrencyStamp(
        string? initialReceiptNumber)
    {
        await using var context = new ApplicationTestContext();
        var (receipt, invoice, item) = CreateInvoiceItem(initialReceiptNumber);
        var originalStamp = receipt.ConcurrencyStamp;
        var itemRepository = Substitute.For<IRepositoryAsync<GoodsReceiptItem, Guid>>();
        var receiptRepository = Substitute.For<IRepositoryAsync<GoodsReceipt, Guid>>();
        context.UnitOfWork.Repository<GoodsReceiptItem>().Returns(itemRepository);
        context.UnitOfWork.Repository<GoodsReceipt>().Returns(receiptRepository);
        itemRepository.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        receiptRepository.GetByIdAsync(receipt.Id, Arg.Any<CancellationToken>()).Returns(receipt);

        var result = await context.Sender.Send(new UpdateInvoiceItemCommand(
            invoice.Id,
            item.Id,
            "  REC-100  ",
            originalStamp));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("REC-100", item.ReceiptNumber);
        Assert.Equal("REC-100", result.Data.ReceiptNumber);
        Assert.NotEqual(originalStamp, result.Data.ConcurrencyStamp);
        await context.UnitOfWork.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WhenInvoiceDoesNotOwnItem_ReturnsNotFoundWithoutSaving()
    {
        await using var context = new ApplicationTestContext();
        var (receipt, _, item) = CreateInvoiceItem();
        var itemRepository = Substitute.For<IRepositoryAsync<GoodsReceiptItem, Guid>>();
        context.UnitOfWork.Repository<GoodsReceiptItem>().Returns(itemRepository);
        itemRepository.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);

        var result = await context.Sender.Send(new UpdateInvoiceItemCommand(
            Guid.NewGuid(),
            item.Id,
            "REC-100",
            receipt.ConcurrencyStamp));

        Assert.False(result.IsSuccess);
        Assert.Equal(["ردیف فاکتور پیدا نشد."], result.Messages);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithStaleConcurrencyStamp_ReturnsConflictWithoutSaving()
    {
        await using var context = new ApplicationTestContext();
        var (receipt, invoice, item) = CreateInvoiceItem();
        var itemRepository = Substitute.For<IRepositoryAsync<GoodsReceiptItem, Guid>>();
        var receiptRepository = Substitute.For<IRepositoryAsync<GoodsReceipt, Guid>>();
        context.UnitOfWork.Repository<GoodsReceiptItem>().Returns(itemRepository);
        context.UnitOfWork.Repository<GoodsReceipt>().Returns(receiptRepository);
        itemRepository.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        receiptRepository.GetByIdAsync(receipt.Id, Arg.Any<CancellationToken>()).Returns(receipt);

        var result = await context.Sender.Send(new UpdateInvoiceItemCommand(
            invoice.Id,
            item.Id,
            "REC-100",
            Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Contains(ConcurrencyMessages.Conflict, result.Messages);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithReceiptNumberOverOneHundredCharacters_IsRejected()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(
            new UpdateInvoiceItemCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new string('R', 101),
                Guid.NewGuid())));
    }

    private static (GoodsReceipt Receipt, Invoice Invoice, GoodsReceiptItem Item) CreateInvoiceItem(
        string? receiptNumber = null)
    {
        var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, null, Guid.NewGuid(), "1");
        var invoice = new Invoice("INV-1", null);
        receipt.AddItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            InvoiceStatus.HasInvoice,
            invoice,
            receiptNumber,
            2,
            null,
            null,
            0);
        return (receipt, invoice, receipt.Items.Single());
    }
}
