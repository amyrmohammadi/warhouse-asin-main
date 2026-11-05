using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.Invoices.Commands.CreateInvoice;
using Warehouse.Application.Features.Inventory.Invoices.Commands.PrintInvoices;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.Invoices;

public sealed class CreateInvoiceTests
{
    [Fact]
    public async Task CreateWithoutGoods_CanBeSentToFinanceThroughTheExistingPrintFlow()
    {
        await using var context = new ApplicationTestContext();
        var supplier = new Supplier("طرف حساب", null);
        var invoices = new List<Invoice>();
        Register(context, invoices, supplier);
        context.InvoicePdfService.GenerateAsync(Arg.Any<IReadOnlyList<InvoicePrintEntry>>(), Arg.Any<CancellationToken>())
            .Returns([1, 2, 3]);

        var result = await context.Sender.Send(new CreateInvoiceCommand("  INV-NEW  ", supplier.Id,
            " شرح فاکتور ", new DateOnly(2026, 11, 3), Price: 124000));

        Assert.True(result.IsSuccess);
        var invoice = Assert.Single(invoices);
        Assert.Equal("INV-NEW", result.Data!.InvoiceNumber);
        Assert.Equal("شرح فاکتور", result.Data.Description);
        Assert.Equal(124000, result.Data.Price);
        Assert.Equal(supplier.Title, result.Data.Counterparty);
        Assert.Empty(result.Data.Items);
        Assert.Empty(invoice.GoodsReceiptItems);
        Assert.Null(invoice.ReceiptDate);
        var printed = await context.Sender.Send(new PrintInvoicesCommand([invoice.Id], true));
        Assert.True(printed.IsSuccess);
        Assert.Equal(InvoiceProcessingStatus.SentToFinance, invoice.ProcessingStatus);
        Assert.NotNull(invoice.FinanceDeliveryDate);
        Assert.True(invoice.IsLocked);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateInvoice_IsRejectedWithoutMergingOrChangingTheOriginal(bool linkedToReceiptDay)
    {
        await using var context = new ApplicationTestContext();
        var supplier = new Supplier("طرف حساب", null);
        var existing = new Invoice("INV-1", supplier.Id, linkedToReceiptDay ? new DateOnly(2026, 10, 19) : null);
        existing.UpdateDetails("اصلی", null, InvoiceProcessingStatus.SentToFinance);
        var invoices = new List<Invoice> { existing };
        Register(context, invoices, supplier);

        var result = await context.Sender.Send(new CreateInvoiceCommand(" INV-1 ", supplier.Id, "جدید", null));
        Assert.False(result.IsSuccess);
        Assert.Single(invoices);
        Assert.Equal("اصلی", existing.Description);
        Assert.Equal(InvoiceProcessingStatus.SentToFinance, existing.ProcessingStatus);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidSupplier_IsRejectedBeforeSaving()
    {
        await using var context = new ApplicationTestContext();
        var supplier = new Supplier("طرف حساب", null);
        var invoices = new List<Invoice>();
        Register(context, invoices, supplier);
        var result = await context.Sender.Send(new CreateInvoiceCommand("INV-1", Guid.NewGuid(), null, null));
        Assert.False(result.IsSuccess);
        Assert.Empty(invoices);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("INV-1", -1)]
    [InlineData("INV-1", 10000000000000000d)]
    public async Task InvalidHeader_IsRejected(string number, double price)
    {
        await using var context = new ApplicationTestContext();
        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(
            new CreateInvoiceCommand(number, null, null, null, Price: (decimal)price)));
    }

    private static void Register(ApplicationTestContext context, List<Invoice> invoices, Supplier supplier)
    {
        var repository = Substitute.For<IRepositoryAsync<Invoice, Guid>>();
        repository.Entities.Returns(_ => invoices.AsQueryable());
        repository.CountAsync(Arg.Any<IQueryable<Invoice>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<Invoice>>().Count());
        repository.AddAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var invoice = call.Arg<Invoice>();
            typeof(Invoice).GetProperty(nameof(Invoice.Supplier))!.SetValue(invoice, supplier);
            invoices.Add(invoice);
            return invoice;
        });
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => invoices.SingleOrDefault(invoice => invoice.Id == call.Arg<Guid>()));
        repository.ToListAsync(Arg.Any<IQueryable<InvoiceDetails>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<InvoiceDetails>>().ToList());
        repository.ToListAsync(Arg.Any<IQueryable<InvoiceSummary>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<InvoiceSummary>>().ToList());
        context.UnitOfWork.Repository<Invoice>().Returns(repository);
        var suppliers = Substitute.For<IRepositoryAsync<Supplier, Guid>>();
        suppliers.GetByIdAsync(supplier.Id, Arg.Any<CancellationToken>()).Returns(supplier);
        context.UnitOfWork.Repository<Supplier>().Returns(suppliers);
    }
}
