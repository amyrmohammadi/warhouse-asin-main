using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.Invoices.Commands.DeleteInvoice;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.Invoices;

public sealed class DeleteInvoiceTests
{
    [Theory]
    [InlineData(InvoiceProcessingStatus.UnderReview, false, true)]
    [InlineData(InvoiceProcessingStatus.UnderReview, true, false)]
    [InlineData(InvoiceProcessingStatus.SentToFinance, false, false)]
    [InlineData(InvoiceProcessingStatus.SentToFinance, true, false)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved, false, false)]
    public async Task Delete_RequiresEmptyInvoiceAndUnderReviewStatus(InvoiceProcessingStatus status, bool hasItem, bool allowed)
    {
        await using var context = new ApplicationTestContext();
        var invoice = new Invoice("INV-1", null);
        invoice.UpdatePrice(100);
        invoice.UpdateDetails("Details", null, status);
        var invoices = Substitute.For<IRepositoryAsync<Invoice, Guid>>();
        invoices.GetByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);
        context.UnitOfWork.Repository<Invoice>().Returns(invoices);
        var items = Substitute.For<IRepositoryAsync<GoodsReceiptItem, Guid>>();
        var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, null, Guid.NewGuid(), "1");
        if (hasItem) receipt.AddItem(Guid.NewGuid(), Guid.NewGuid(), InvoiceStatus.HasInvoice, invoice, null, 1, null, null, 0);
        items.Entities.Returns(receipt.Items.AsQueryable());
        items.CountAsync(Arg.Any<IQueryable<GoodsReceiptItem>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<GoodsReceiptItem>>().Count());
        context.UnitOfWork.Repository<GoodsReceiptItem>().Returns(items);
        var result = await context.Sender.Send(new DeleteInvoiceCommand(invoice.Id));
        Assert.Equal(allowed, result.IsSuccess);
        if (allowed)
        {
            await invoices.Received(1).DeleteAsync(invoice, Arg.Any<CancellationToken>());
            await context.UnitOfWork.Received(1).SaveAsync(Arg.Any<CancellationToken>());
            await context.UnitOfWork.Received(1).CommitTransaction(Arg.Any<CancellationToken>());
        }
        else
        {
            await invoices.DidNotReceive().DeleteAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>());
            await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task Delete_MissingInvoiceDoesNotWrite()
    {
        await using var context = new ApplicationTestContext();
        context.UnitOfWork.Repository<Invoice>().Returns(Substitute.For<IRepositoryAsync<Invoice, Guid>>());
        var result = await context.Sender.Send(new DeleteInvoiceCommand(Guid.NewGuid()));
        Assert.False(result.IsSuccess);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }
}
