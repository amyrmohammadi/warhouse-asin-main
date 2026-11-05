using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoiceById;
using Warehouse.Application.Features.Inventory.Invoices.Queries.GetInvoices;
using Warehouse.Application.Tests.TestSupport;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.Invoices;

public sealed class GetInvoicesFeatureTests
{
    [Theory]
    [InlineData(InvoiceProcessingStatus.UnderReview, true)]
    [InlineData(InvoiceProcessingStatus.SentToFinance, false)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved, false)]
    public async Task GetInvoices_PreservesDeleteEligibility(InvoiceProcessingStatus status, bool expected)
    {
        await using var context = new ApplicationTestContext();
        var invoice = new Invoice("INV-EMPTY", null);
        invoice.UpdateDetails(null, null, status);
        context.UnitOfWork.Repository<Invoice>().Returns(CreateRepository([invoice]));

        var result = await context.Sender.Send(new GetInvoicesQuery());

        Assert.Equal(expected, Assert.Single(result.Data!.Items).CanDelete);
    }

    [Theory]
    [InlineData(DateOperator.Eq, 1, false)]
    [InlineData(DateOperator.Before, 2, false)]
    [InlineData(DateOperator.After, 2, false)]
    [InlineData(DateOperator.Between, 2, false)]
    [InlineData(DateOperator.Eq, 1, true)]
    [InlineData(DateOperator.Before, 2, true)]
    [InlineData(DateOperator.After, 2, true)]
    [InlineData(DateOperator.Between, 2, true)]
    public async Task GetInvoices_FiltersDates_InclusivelyAndExcludesUnsetDates(DateOperator operation, int expected, bool useInvoiceDate)
    {
        await using var context = new ApplicationTestContext();
        var values = Enumerable.Range(1, 4).Select(index => new Invoice($"INV-{index}", null)).ToArray();
        for (var index = 0; index < 3; index++)
        {
            var date = new DateOnly(2026, 11, index + 1);
            if (useInvoiceDate) values[index].UpdateDetails(null, date, InvoiceProcessingStatus.UnderReview);
            else values[index].UpdateFinanceDeliveryDate(date);
        }
        var repository = CreateRepository(values);
        context.UnitOfWork.Repository<Invoice>().Returns(repository);
        var filter = new DateFilter
        {
            Operator = operation, Value = new DateOnly(2026, 11, 2),
            ValueTo = new DateOnly(2026, 11, 3), UtcOffsetMinutes = 210
        };
        var result = await context.Sender.Send(new GetInvoicesQuery
        {
            FinanceDeliveryDate = useInvoiceDate ? null : filter,
            InvoiceDate = useInvoiceDate ? filter : null
        });
        Assert.Equal(4, result.Data!.TotalCount);
        Assert.Equal(expected, result.Data.FilteredCount);
        Assert.DoesNotContain(result.Data.Items, invoice => (useInvoiceDate ? invoice.InvoiceDate : invoice.FinanceDeliveryDate) == null);
        if (operation == DateOperator.Eq) Assert.Equal(values[1].Id, Assert.Single(result.Data.Items).Id);
    }

    [Theory]
    [InlineData(InvoiceProcessingStatus.UnderReview)]
    [InlineData(InvoiceProcessingStatus.SentToFinance)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved)]
    public async Task GetInvoices_CombinesStatusAndDeliveryDate_BeforePagination(InvoiceProcessingStatus status)
    {
        await using var context = new ApplicationTestContext();
        var day = new DateOnly(2026, 11, 3);
        var values = Enum.GetValues<InvoiceProcessingStatus>().SelectMany(value => Enumerable.Range(1, 3).Select(index =>
        {
            var invoice = new Invoice($"INV-{value}-{index}", null);
            invoice.UpdateDetails(null, day, value);
            invoice.UpdateFinanceDeliveryDate(index == 3 ? day.AddDays(-1) : day);
            return invoice;
        })).ToArray();
        var repository = CreateRepository(values);
        context.UnitOfWork.Repository<Invoice>().Returns(repository);
        var result = await context.Sender.Send(new GetInvoicesQuery
        {
            ProcessingStatus = status, FinanceDeliveryDate = new DateFilter { Operator = DateOperator.Eq, Value = day },
            InvoiceDate = new DateFilter { Operator = DateOperator.Eq, Value = day },
            PageNumber = 2, PageSize = 1, SortBy = "invoiceNumber"
        });
        Assert.Equal(9, result.Data!.TotalCount);
        Assert.Equal(2, result.Data.FilteredCount);
        var item = Assert.Single(result.Data.Items);
        Assert.Equal($"INV-{status}-2", item.InvoiceNumber);
        Assert.Equal(status, item.ProcessingStatus);
        Assert.Equal(day, item.FinanceDeliveryDate);
    }

    [Fact]
    public async Task GetInvoices_RejectsInvalidStatusAndIncompleteDateRanges()
    {
        await using var context = new ApplicationTestContext();
        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(new GetInvoicesQuery
            { ProcessingStatus = (InvoiceProcessingStatus)999 }));
        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(new GetInvoicesQuery
            { FinanceDeliveryDate = new DateFilter { Operator = DateOperator.Between, Value = new DateOnly(2026, 11, 3) } }));
        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(new GetInvoicesQuery
            { InvoiceDate = new DateFilter { Operator = DateOperator.Between, Value = new DateOnly(2026, 11, 3), ValueTo = new DateOnly(2026, 11, 2) } }));
    }

    [Theory]
    [InlineData("receiptNumber", "REC-MATCH", true)]
    [InlineData("receiptNumber", "MATCH", false)]
    [InlineData("receiptNumber", "MATCH", true)]
    [InlineData("productTitle", "کالای ویژه", true)]
    [InlineData("productTitle", "ویژه", false)]
    [InlineData("search", "REC-MATCH", false)]
    [InlineData("search", "کالای ویژه", false)]
    [InlineData("search", "CODE-MATCH", false)]
    public async Task GetInvoices_FiltersReceiptNumberAndProduct(string field, string value, bool exact)
    {
        await using var context = new ApplicationTestContext();
        var matching = new Invoice("INV-1", null);
        var other = new Invoice("INV-2", null);
        var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, null, Guid.NewGuid(), "1");
        AddInvoiceItem(receipt, matching, "REC-MATCH", 0);
        AddInvoiceItem(receipt, other, null, 1);
        var product = new Product("کالای ویژه", "CODE-MATCH", Guid.NewGuid(), null);
        typeof(GoodsReceiptItem).GetProperty(nameof(GoodsReceiptItem.Product))!.SetValue(matching.GoodsReceiptItems.Single(), product);
        var repository = CreateRepository(other, matching);
        context.UnitOfWork.Repository<Invoice>().Returns(repository);
        var filter = new TextFilter { Value = value, Operator = exact ? TextOperator.Eq : TextOperator.Contains };
        var result = await context.Sender.Send(new GetInvoicesQuery
        {
            ReceiptNumber = field == "receiptNumber" ? filter : null,
            ProductTitle = field == "productTitle" ? filter : null,
            Search = field == "search" ? value : null
        });
        Assert.Equal(2, result.Data!.TotalCount);
        var expected = exact && value == "MATCH" ? 0 : 1;
        Assert.Equal(expected, result.Data.FilteredCount);
        if (expected == 1) Assert.Equal(matching.Id, Assert.Single(result.Data.Items).Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("createdAtUtc")]
    public async Task GetInvoices_ShowsNewestCreatedFirst_BeforePagination(string? sortBy)
    {
        await using var context = new ApplicationTestContext();
        var oldest = new Invoice("INV-999", null);
        var middle = new Invoice("INV-500", null);
        var newest = new Invoice("INV-001", null);
        var start = new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);
        typeof(Invoice).GetProperty(nameof(Invoice.CreatedAtUtc))!.SetValue(oldest, start);
        typeof(Invoice).GetProperty(nameof(Invoice.CreatedAtUtc))!.SetValue(middle, start.AddMinutes(1));
        typeof(Invoice).GetProperty(nameof(Invoice.CreatedAtUtc))!.SetValue(newest, start.AddMinutes(2));
        // Editing a document's dates, amount or number must not move it to the top.
        oldest.UpdateDetails("ویرایش", new DateOnly(2026, 12, 1), InvoiceProcessingStatus.SentToFinance);
        oldest.UpdateFinanceDeliveryDate(new DateOnly(2026, 12, 2));
        oldest.UpdatePrice(1000);
        newest.UpdateHeader("INV-000", null);
        var repository = CreateRepository(oldest, newest, middle);
        context.UnitOfWork.Repository<Invoice>().Returns(repository);

        var first = await context.Sender.Send(new GetInvoicesQuery { PageSize = 2, SortBy = sortBy, SortDescending = true });
        var second = await context.Sender.Send(new GetInvoicesQuery { PageNumber = 2, PageSize = 2, SortBy = sortBy, SortDescending = true });

        Assert.Equal([newest.Id, middle.Id], first.Data!.Items.Select(invoice => invoice.Id));
        Assert.Equal(oldest.Id, Assert.Single(second.Data!.Items).Id);
        Assert.Equal(start, oldest.CreatedAtUtc);
    }

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
