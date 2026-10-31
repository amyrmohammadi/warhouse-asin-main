using ClosedXML.Excel;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.DeleteGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.ImportGoodsReceipts;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.LinkGoodsIssueItem;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.UpdateGoodsReceipt;
using Warehouse.Application.Features.Inventory.Invoices.Commands.ChangeInvoiceStatus;
using Warehouse.Application.Features.Inventory.Invoices.Commands.UpdateInvoice;
using Warehouse.Application.Features.Inventory.Invoices.Commands.UpdateInvoiceDetails;
using Warehouse.Application.Features.Inventory.Invoices.Commands.UpdateInvoiceItem;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.Invoices;

public sealed class InvoiceLockTests
{
    [Theory]
    [InlineData(InvoiceProcessingStatus.SentToFinance)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved)]
    public async Task LockedInvoice_BlocksEveryInvoiceAndReceiptMutation(InvoiceProcessingStatus status)
    {
        await using var fixture = new Fixture(status);
        var context = fixture.Context;
        var invoice = fixture.Invoice;
        var receipt = fixture.Receipt;
        var item = fixture.Item;
        var stamp = receipt.ConcurrencyStamp;
        var results = new Result[]
        {
            await context.Sender.Send(new UpdateInvoiceCommand(invoice.Id, "CHANGED", null, "changed", null,
                InvoiceProcessingStatus.UnderReview, [])),
            await context.Sender.Send(new UpdateInvoiceDetailsCommand(invoice.Id, "changed", null, InvoiceProcessingStatus.UnderReview)),
            await context.Sender.Send(new UpdateInvoiceItemCommand(invoice.Id, item.Id, "NEW", stamp)),
            await context.Sender.Send(new UpdateGoodsReceiptCommand(receipt.Id, fixture.Values(), true, true, true, stamp, true)),
            await context.Sender.Send(new DeleteGoodsReceiptCommand(receipt.Id)),
            await context.Sender.Send(new LinkGoodsIssueItemCommand(item.Id, null, stamp))
        };
        Assert.All(results, result =>
        {
            Assert.False(result.IsSuccess);
            Assert.Contains(Invoice.LockedMessage, result.Messages);
        });
        Assert.Equal("INV-1", invoice.InvoiceNumber);
        Assert.Equal(status, invoice.ProcessingStatus);
        Assert.Equal("REC-1", item.ReceiptNumber);
        Assert.Equal(stamp, receipt.ConcurrencyStamp);
        Assert.Single(fixture.Receipts.Entities);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(InvoiceProcessingStatus.SentToFinance, false)]
    [InlineData(InvoiceProcessingStatus.SentToFinance, true)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved, false)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved, true)]
    public async Task Reopen_RequiresDedicatedPermission_AndOnlyChangesStatus(InvoiceProcessingStatus original, bool allowed)
    {
        await using var fixture = new Fixture(original);
        var context = fixture.Context;
        var result = await context.Sender.Send(new ChangeInvoiceStatusCommand(fixture.Invoice.Id,
            InvoiceProcessingStatus.UnderReview, original, CanReopen: allowed));
        Assert.Equal(allowed, result.IsSuccess);
        Assert.Equal(allowed ? InvoiceProcessingStatus.UnderReview : original, fixture.Invoice.ProcessingStatus);
        Assert.Equal(!allowed, fixture.Invoice.IsLocked);
        Assert.Equal("INV-1", fixture.Invoice.InvoiceNumber);
        Assert.Equal(100, fixture.Invoice.Price);
        Assert.Equal(new DateOnly(2026, 10, 30), fixture.Invoice.FinanceDeliveryDate);
        Assert.Equal("REC-1", fixture.Item.ReceiptNumber);
        await context.UnitOfWork.Received(allowed ? 1 : 0).SaveAsync(Arg.Any<CancellationToken>());
        if (allowed)
        {
            var edited = await context.Sender.Send(new UpdateInvoiceItemCommand(fixture.Invoice.Id, fixture.Item.Id,
                "AFTER-REOPEN", fixture.Receipt.ConcurrencyStamp));
            Assert.True(edited.IsSuccess);
            Assert.Equal("AFTER-REOPEN", fixture.Item.ReceiptNumber);
        }
    }

    [Fact]
    public async Task Approve_UsesStatusOnlyOperation_AndKeepsDocumentLocked()
    {
        await using var fixture = new Fixture(InvoiceProcessingStatus.SentToFinance);
        var result = await fixture.Context.Sender.Send(new ChangeInvoiceStatusCommand(fixture.Invoice.Id,
            InvoiceProcessingStatus.FinanceApproved, InvoiceProcessingStatus.SentToFinance, CanApprove: true));
        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.IsLocked);
        Assert.Equal(InvoiceProcessingStatus.FinanceApproved, fixture.Invoice.ProcessingStatus);
        Assert.Equal("INV-1", fixture.Invoice.InvoiceNumber);
    }

    [Fact]
    public async Task StatusChange_RejectsStaleExpectedState()
    {
        await using var fixture = new Fixture(InvoiceProcessingStatus.FinanceApproved);
        var result = await fixture.Context.Sender.Send(new ChangeInvoiceStatusCommand(fixture.Invoice.Id,
            InvoiceProcessingStatus.UnderReview, InvoiceProcessingStatus.SentToFinance, CanReopen: true));
        Assert.False(result.IsSuccess);
        Assert.Contains(ConcurrencyMessages.Conflict, result.Messages);
        await fixture.Context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(InvoiceProcessingStatus.SentToFinance)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved)]
    public async Task Create_CannotAttachNewGoodsToLockedInvoice(InvoiceProcessingStatus status)
    {
        await using var fixture = new Fixture(status);
        var result = await fixture.Context.Sender.Send(fixture.Values());
        Assert.False(result.IsSuccess);
        Assert.Contains(Invoice.LockedMessage, result.Messages);
        await fixture.Context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
        Assert.Single(fixture.Receipts.Entities);
    }

    [Theory]
    [InlineData(InvoiceProcessingStatus.SentToFinance)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved)]
    public async Task Import_CannotAttachNewGoodsToLockedInvoice(InvoiceProcessingStatus status)
    {
        await using var fixture = new Fixture(status);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("ورود کالا");
        sheet.Cell(1, 1).Value = "ردیف";
        sheet.Cell(2, 2).Value = "2026/10/30";
        sheet.Cell(2, 4).Value = fixture.Product.Title;
        sheet.Cell(2, 5).Value = fixture.Product.Code;
        sheet.Cell(2, 6).Value = 2;
        sheet.Cell(2, 7).Value = fixture.Unit.Title;
        sheet.Cell(2, 9).Value = fixture.Person.FullName;
        sheet.Cell(2, 10).Value = "دارد";
        sheet.Cell(2, 11).Value = fixture.Invoice.InvoiceNumber;
        sheet.Cell(2, 12).Value = fixture.Supplier.Title;
        sheet.Cell(2, 14).Value = fixture.Warehouse.Title;
        sheet.Cell(2, 15).Value = "2";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var result = await fixture.Context.Sender.Send(new ImportGoodsReceiptsCommand("receipts.xlsx", stream.ToArray(), true, true));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages, message => message.Contains(Invoice.LockedMessage));
        await fixture.Context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationTestContext Context { get; } = new();
        public Invoice Invoice { get; }
        public GoodsReceipt Receipt { get; }
        public GoodsReceiptItem Item => Receipt.Items.Single();
        public Supplier Supplier { get; } = new("طرف حساب", null);
        public UnitOfMeasure Unit { get; } = new("عدد", null);
        public StorageWarehouse Warehouse { get; } = new("انبار", null);
        public Person Person { get; } = new("تحویل‌دهنده", "");
        public Product Product { get; }
        public MemoryRepository<GoodsReceipt> Receipts { get; }
        public Fixture(InvoiceProcessingStatus status)
        {
            Product = new("کالا", "P-1", Unit.Id, null);
            Set(Product, nameof(Product.UnitOfMeasure), Unit);
            Invoice = new("INV-1", Supplier.Id, new DateOnly(2026, 10, 30));
            Invoice.UpdatePrice(100);
            Invoice.UpdateFinanceDeliveryDate(new DateOnly(2026, 10, 30));
            Invoice.UpdateDetails("شرح", new DateOnly(2026, 10, 29), status);
            Set(Invoice, nameof(Invoice.Supplier), Supplier);
            Receipt = new(new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero), null, Person.Id, "1");
            Receipt.AddItem(Product.Id, Warehouse.Id, InvoiceStatus.HasInvoice, Invoice, "REC-1", 1, null, null, 0);
            Set(Receipt, nameof(GoodsReceipt.Deliverer), Person);
            Set(Item, nameof(GoodsReceiptItem.GoodsReceipt), Receipt);
            Set(Item, nameof(GoodsReceiptItem.Product), Product);
            Set(Item, nameof(GoodsReceiptItem.Warehouse), Warehouse);
            Invoice.GoodsReceiptItems.Add(Item);
            Register([Invoice]); Register([Item]); Register([Product]); Register([Unit]); Register([Warehouse]);
            Register([Supplier]); Register([Person]); Register<GoodsIssue>([]); Register<GoodsIssueItem>([]);
            Receipts = Register([Receipt]);
        }
        public CreateGoodsReceiptCommand Values() => new(Receipt.EnteredAtUtc, null, Person.Id, null, "2",
            [new(Product.Id, null, null, null, Warehouse.Id, InvoiceStatus.HasInvoice, "INV-1", Supplier.Id, null, "NEW", 2, null, null)]);
        private MemoryRepository<T> Register<T>(List<T> items) where T : Entity<Guid>
        {
            var repository = new MemoryRepository<T>(items);
            Context.UnitOfWork.Repository<T>().Returns(repository);
            return repository;
        }
        public ValueTask DisposeAsync() => Context.DisposeAsync();
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name)!.SetValue(target, value);
    }

    private sealed class MemoryRepository<T>(List<T> items) : IRepositoryAsync<T, Guid> where T : Entity<Guid>
    {
        public IQueryable<T> Entities => items.AsQueryable();
        public Task<T?> GetByIdAsync(Guid id, CancellationToken token = default) => Task.FromResult(items.FirstOrDefault(item => item.Id == id));
        public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken token = default) => Task.FromResult<IReadOnlyList<T>>(items);
        public Task<IReadOnlyList<T>> GetPagedResponseAsync(int page, int size, CancellationToken token = default) => Task.FromResult<IReadOnlyList<T>>(items.Skip((page - 1) * size).Take(size).ToList());
        public Task<int> CountAsync(IQueryable<T> query, CancellationToken token = default) => Task.FromResult(query.Count());
        public Task<IReadOnlyList<T>> ToListAsync(IQueryable<T> query, CancellationToken token = default) => Task.FromResult<IReadOnlyList<T>>(query.ToList());
        public Task<IReadOnlyList<TResult>> ToListAsync<TResult>(IQueryable<TResult> query, CancellationToken token = default) => Task.FromResult<IReadOnlyList<TResult>>(query.ToList());
        public Task<T> AddAsync(T item, CancellationToken token = default) { items.Add(item); return Task.FromResult(item); }
        public Task UpdateAsync(T item, CancellationToken token = default) => Task.CompletedTask;
        public Task DeleteAsync(T item, CancellationToken token = default) { items.Remove(item); return Task.CompletedTask; }
    }
}
