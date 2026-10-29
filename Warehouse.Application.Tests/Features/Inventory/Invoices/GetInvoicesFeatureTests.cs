using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoiceById;
using Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoices;
using Warehouse.Application.Tests.TestSupport;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.Invoices;

public sealed class GetInvoicesFeatureTests
{
    [Fact]
    public async Task GetInvoices_SearchesAndSortsByInvoiceNumber()
    {
        await using var context = new ApplicationTestContext();
        var repository = CreateRepository(
            new Invoice("INV-20", null),
            new Invoice("INV-10", null),
            new Invoice("OTHER-1", null));
        context.UnitOfWork.Repository<Invoice>().Returns(repository);

        var result = await context.Sender.Send(new GetInvoicesQuery
        {
            Search = "  INV-  ",
            PageNumber = 1,
            PageSize = 10,
            SortBy = "invoiceNumber"
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.TotalCount);
        Assert.Equal(2, result.Data.FilteredCount);
        Assert.Equal(["INV-10", "INV-20"], result.Data.Items.Select(item => item.InvoiceNumber));
    }

    [Fact]
    public async Task GetInvoiceById_ReturnsInvoiceWithItsItemsCollection()
    {
        await using var context = new ApplicationTestContext();
        var invoice = new Invoice("INV-1", null);
        invoice.UpdateDetails("  توضیحات فاکتور  ", new DateOnly(2026, 10, 20),
            InvoiceProcessingStatus.SentToFinance);
        invoice.UpdatePrice(1250);
        var repository = CreateRepository(invoice);
        context.UnitOfWork.Repository<Invoice>().Returns(repository);

        var result = await context.Sender.Send(new GetInvoiceByIdQuery(invoice.Id));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(invoice.Id, result.Data.Id);
        Assert.Equal("INV-1", result.Data.InvoiceNumber);
        Assert.Equal(1250, result.Data.Price);
        Assert.Equal("توضیحات فاکتور", result.Data.Description);
        Assert.Equal(new DateOnly(2026, 10, 20), result.Data.InvoiceDate);
        Assert.Equal(InvoiceProcessingStatus.SentToFinance, result.Data.ProcessingStatus);
        Assert.Empty(result.Data.Items);
    }

    [Fact]
    public async Task GetInvoices_ReturnsDistinctReceiptNumbersAndMissingReceiptItemCount()
    {
        await using var context = new ApplicationTestContext();
        var invoice = new Invoice("INV-1", null);
        Assert.Equal(InvoiceProcessingStatus.UnderReview, invoice.ProcessingStatus);
        invoice.UpdateDetails("شرح", new DateOnly(2026, 10, 20),
            InvoiceProcessingStatus.FinanceApproved);
        invoice.UpdatePrice(900);
        var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, null, Guid.NewGuid(), "1");
        AddInvoiceItem(receipt, invoice, "REC-2", 0);
        AddInvoiceItem(receipt, invoice, "REC-1", 1);
        AddInvoiceItem(receipt, invoice, "REC-2", 2);
        AddInvoiceItem(receipt, invoice, null, 3);
        var repository = CreateRepository(invoice);
        context.UnitOfWork.Repository<Invoice>().Returns(repository);

        var result = await context.Sender.Send(new GetInvoicesQuery());

        Assert.True(result.IsSuccess);
        var summary = Assert.Single(result.Data!.Items);
        Assert.Equal(["REC-1", "REC-2"], summary.ReceiptNumbers);
        Assert.Equal(1, summary.ItemsWithoutReceiptCount);
        Assert.Equal("شرح", summary.Description);
        Assert.Equal(900, summary.Price);
        Assert.Equal(new DateOnly(2026, 10, 20), summary.InvoiceDate);
        Assert.Equal(InvoiceProcessingStatus.FinanceApproved, summary.ProcessingStatus);
    }

    [Fact]
    public async Task GetInvoices_WithInvalidPageSize_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(new GetInvoicesQuery { PageSize = 101 }));
    }

    private static IRepositoryAsync<Invoice, Guid> CreateRepository(params Invoice[] invoices)
    {
        var repository = Substitute.For<IRepositoryAsync<Invoice, Guid>>();
        repository.Entities.Returns(invoices.AsQueryable());
        repository.CountAsync(Arg.Any<IQueryable<Invoice>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<Invoice>>().Count());
        repository.ToListAsync(
                Arg.Any<IQueryable<InvoiceSummary>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<InvoiceSummary>>().ToList());
        repository.ToListAsync(
                Arg.Any<IQueryable<InvoiceDetails>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<InvoiceDetails>>().ToList());
        return repository;
    }

    private static void AddInvoiceItem(
        GoodsReceipt receipt,
        Invoice invoice,
        string? receiptNumber,
        int sortOrder)
    {
        receipt.AddItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Domain.Enums.InvoiceStatus.HasInvoice,
            invoice,
            receiptNumber,
            1,
            null,
            null,
            sortOrder);
        invoice.GoodsReceiptItems.Add(receipt.Items.Last());
    }
}
