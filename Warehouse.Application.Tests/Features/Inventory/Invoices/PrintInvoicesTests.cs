using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.Invoices.Commands.PrintInvoices;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.Invoices;

public sealed class PrintInvoicesTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Print_UsesSelectedOrder_AndOnlyUpdatesFinanceWhenRequested(bool send)
    {
        await using var context = new ApplicationTestContext(new Clock());
        var first = Invoice("INV-1", 100, true);
        var second = Invoice("INV-2", 200, true);
        var unselected = Invoice("OTHER", 300, true);
        var oldDate = new DateOnly(2026, 10, 20);
        first.UpdateFinanceDeliveryDate(oldDate);
        Register(context, [first, second, unselected]);
        context.InvoicePdfService.GenerateAsync(Arg.Any<IReadOnlyList<InvoicePrintEntry>>(), Arg.Any<CancellationToken>())
            .Returns([1, 2, 3]);

        var result = await context.Sender.Send(new PrintInvoicesCommand([second.Id, first.Id], send));

        Assert.True(result.IsSuccess);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Data);
        var today = new DateOnly(2026, 10, 31);
        Assert.Equal(send ? InvoiceProcessingStatus.SentToFinance : InvoiceProcessingStatus.UnderReview, first.ProcessingStatus);
        Assert.Equal(send ? today : oldDate, first.FinanceDeliveryDate);
        Assert.Equal(send ? today : null, second.FinanceDeliveryDate);
        Assert.Equal(InvoiceProcessingStatus.UnderReview, unselected.ProcessingStatus);
        await context.InvoicePdfService.Received(1).GenerateAsync(Arg.Is<IReadOnlyList<InvoicePrintEntry>>(rows =>
            rows.Count == 2 && rows[0].InvoiceNumber == "INV-2" && rows[1].InvoiceNumber == "INV-1" &&
            rows[0].FinanceDeliveryDate == (send ? today : null)), Arg.Any<CancellationToken>());
        await context.UnitOfWork.Received(send ? 1 : 0).SaveAsync(Arg.Any<CancellationToken>());
        await context.UnitOfWork.Received(1).CommitTransaction(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, true, "مبلغ")]
    [InlineData(true, false, "طرف حساب")]
    [InlineData(false, false, "طرف حساب")]
    public async Task MissingRequiredData_BlocksEntireBatchBeforeGeneratingOrWriting(bool price, bool supplier, string message)
    {
        await using var context = new ApplicationTestContext();
        var valid = Invoice("VALID", 100, true);
        var invalid = Invoice("INCOMPLETE", price ? 100 : null, supplier);
        Register(context, [valid, invalid]);
        var result = await context.Sender.Send(new PrintInvoicesCommand([valid.Id, invalid.Id], true));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages, value => value.Contains("INCOMPLETE") && value.Contains(message));
        Assert.Equal(InvoiceProcessingStatus.UnderReview, valid.ProcessingStatus);
        await context.InvoicePdfService.DidNotReceive().GenerateAsync(Arg.Any<IReadOnlyList<InvoicePrintEntry>>(), Arg.Any<CancellationToken>());
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
        await context.UnitOfWork.Received(1).RollBackTransaction(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConversionFailure_DoesNotChangeStatusesOrDates()
    {
        await using var context = new ApplicationTestContext();
        var invoice = Invoice("INV-1", 100, true);
        Register(context, [invoice]);
        context.InvoicePdfService.GenerateAsync(Arg.Any<IReadOnlyList<InvoicePrintEntry>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<byte[]>(new TimeoutException()));
        await Assert.ThrowsAsync<TimeoutException>(() => context.Sender.Send(new PrintInvoicesCommand([invoice.Id], true)));
        Assert.Null(invoice.FinanceDeliveryDate);
        Assert.Equal(InvoiceProcessingStatus.UnderReview, invoice.ProcessingStatus);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
        await context.UnitOfWork.Received(1).RollBackTransaction(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MissingInvoice_BlocksBatch()
    {
        await using var context = new ApplicationTestContext();
        Register(context, []);
        var result = await context.Sender.Send(new PrintInvoicesCommand([Guid.NewGuid()], true));
        Assert.False(result.IsSuccess);
        await context.InvoicePdfService.DidNotReceive().GenerateAsync(Arg.Any<IReadOnlyList<InvoicePrintEntry>>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(InvoiceProcessingStatus.SentToFinance)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved)]
    public async Task LockedInvoice_CanPrintWithoutChangingItsStatus(InvoiceProcessingStatus status)
    {
        await using var context = new ApplicationTestContext();
        var invoice = Invoice("APPROVED", 100, true);
        invoice.UpdateDetails(null, null, status);
        Register(context, [invoice]);
        context.InvoicePdfService.GenerateAsync(Arg.Any<IReadOnlyList<InvoicePrintEntry>>(), Arg.Any<CancellationToken>()).Returns([1]);
        var blocked = await context.Sender.Send(new PrintInvoicesCommand([invoice.Id], true));
        Assert.False(blocked.IsSuccess);
        var printed = await context.Sender.Send(new PrintInvoicesCommand([invoice.Id], false));
        Assert.True(printed.IsSuccess);
        Assert.Equal(status, invoice.ProcessingStatus);
    }

    private static Invoice Invoice(string number, decimal? price, bool hasSupplier)
    {
        var supplier = hasSupplier ? new Supplier("طرف حساب", null) : null;
        var invoice = new Invoice(number, supplier?.Id);
        typeof(Invoice).GetProperty(nameof(Domain.Entities.Invoice.Supplier))!.SetValue(invoice, supplier);
        invoice.UpdatePrice(price);
        return invoice;
    }

    private static void Register(ApplicationTestContext context, Invoice[] invoices)
    {
        var repository = Substitute.For<IRepositoryAsync<Invoice, Guid>>();
        repository.Entities.Returns(invoices.AsQueryable());
        repository.ToListAsync(Arg.Any<IQueryable<InvoiceSummary>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<InvoiceSummary>>().ToList());
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => invoices.SingleOrDefault(invoice => invoice.Id == call.Arg<Guid>()));
        context.UnitOfWork.Repository<Invoice>().Returns(repository);
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 30, 22, 0, 0, TimeSpan.Zero);
    }
}
