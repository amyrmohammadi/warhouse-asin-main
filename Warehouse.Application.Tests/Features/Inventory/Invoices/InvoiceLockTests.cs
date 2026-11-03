using ClosedXML.Excel;
using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.DeleteGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.ImportGoodsReceipts;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.LinkGoodsIssueItem;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.UpdateGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptItemRows;
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
    public async Task MixedReceipt_CanEditAndAddOtherItems_KeepingLockedItemUnchanged(InvoiceProcessingStatus status)
    {
        await using var fixture = new Fixture(status);
        var unlocked = fixture.AddUnlockedItem();
        var locked = fixture.Item;
        // The same product occurs twice; row IDs must distinguish the closed and open rows.
        var lockedValues = fixture.StoredValues(locked);
        var unlockedValues = fixture.StoredValues(unlocked) with { Quantity = 5, ReceiptNumber = "EDITED" };
        var addedValues = unlockedValues with { Id = null, Quantity = 3, ReceiptNumber = "ADDED" };
        var values = fixture.Values() with { PageNumber = "1", Items = [unlockedValues, lockedValues, addedValues] };
        var result = await fixture.Context.Sender.Send(new UpdateGoodsReceiptCommand(fixture.Receipt.Id,
            values, true, true, true, fixture.Receipt.ConcurrencyStamp, true));

        Assert.True(result.IsSuccess, string.Join(" ", result.Messages));
        Assert.Equal(1, locked.Quantity);
        Assert.Equal("REC-1", locked.ReceiptNumber);
        Assert.Equal(fixture.Invoice.Id, locked.InvoiceId);
        Assert.Equal(5, unlocked.Quantity);
        Assert.Equal("EDITED", unlocked.ReceiptNumber);
        Assert.Equal(3, fixture.Receipt.Items.Single(item => item.ReceiptNumber == "ADDED").Quantity);
        Assert.True(result.Data!.Items.Single(item => item.Id == locked.Id).IsLocked);
        Assert.False(result.Data.Items.Single(item => item.Id == unlocked.Id).IsLocked);
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("receipt")]
    [InlineData("invoice")]
    [InlineData("supplier")]
    [InlineData("warehouse")]
    [InlineData("description")]
    [InlineData("request")]
    [InlineData("issue")]
    [InlineData("product")]
    [InlineData("delete")]
    [InlineData("date")]
    public async Task MixedReceipt_CannotChangeOrRemoveLockedItem(string change)
    {
        await using var fixture = new Fixture(InvoiceProcessingStatus.SentToFinance);
        var unlocked = fixture.AddUnlockedItem();
        var locked = fixture.StoredValues(fixture.Item);
        locked = change switch
        {
            "quantity" => locked with { Quantity = 9 },
            "receipt" => locked with { ReceiptNumber = "CHANGED" },
            "invoice" => locked with { InvoiceNumber = "OTHER" },
            "supplier" => locked with { SupplierId = null },
            "warehouse" => locked with { WarehouseId = Guid.NewGuid() },
            "description" => locked with { Description = "changed" },
            "request" => locked with { RequestNumber = "changed" },
            "issue" => locked with { IssueNumber = "changed" },
            "product" => locked with { ProductId = Guid.NewGuid() },
            _ => locked
        };
        var values = fixture.Values() with
        {
            PageNumber = "1",
            EnteredAtUtc = change == "date" ? fixture.Receipt.EnteredAtUtc.AddDays(1) : fixture.Receipt.EnteredAtUtc,
            Items = change == "delete" ? [fixture.StoredValues(unlocked)] : [locked, fixture.StoredValues(unlocked)]
        };
        var stamp = fixture.Receipt.ConcurrencyStamp;
        var result = await fixture.Context.Sender.Send(new UpdateGoodsReceiptCommand(fixture.Receipt.Id,
            values, true, true, true, stamp, true));
        Assert.False(result.IsSuccess);
        Assert.Contains(Invoice.LockedMessage, result.Messages);
        Assert.Equal(stamp, fixture.Receipt.ConcurrencyStamp);
        await fixture.Context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MixedReceipt_CanRemoveUnlockedItem_AndKeepLockedItem()
    {
        await using var fixture = new Fixture(InvoiceProcessingStatus.SentToFinance);
        var unlocked = fixture.AddUnlockedItem();
        var values = fixture.Values() with { PageNumber = "1", Items = [fixture.StoredValues(fixture.Item)] };
        var result = await fixture.Context.Sender.Send(new UpdateGoodsReceiptCommand(fixture.Receipt.Id,
            values, true, true, true, fixture.Receipt.ConcurrencyStamp));
        Assert.True(result.IsSuccess, string.Join(" ", result.Messages));
        Assert.DoesNotContain(fixture.Context.UnitOfWork.Repository<GoodsReceiptItem>().Entities, item => item.Id == unlocked.Id);
        Assert.Equal(fixture.Invoice.Id, fixture.Item.InvoiceId);
    }

    [Fact]
    public async Task MixedReceipt_CanLinkUnlockedItem()
    {
        await using var fixture = new Fixture(InvoiceProcessingStatus.SentToFinance);
        var unlocked = fixture.AddUnlockedItem();
        var result = await fixture.Context.Sender.Send(new LinkGoodsIssueItemCommand(unlocked.Id, null,
            fixture.Receipt.ConcurrencyStamp));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task MixedReceipt_ListLocksOnlyItemsBelongingToClosedInvoice()
    {
        await using var fixture = new Fixture(InvoiceProcessingStatus.SentToFinance);
        var unlocked = fixture.AddUnlockedItem();
        var result = await fixture.Context.Sender.Send(new GetGoodsReceiptItemRowsQuery());
        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.Items.Single(item => item.Id == fixture.Item.Id).IsLocked);
        Assert.False(result.Data.Items.Single(item => item.Id == unlocked.Id).IsLocked);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task MixedReceipt_ColumnPermissionsAllowEditingUnlockedRows(bool invoice, bool receipt, bool warehouse)
    {
        await using var fixture = new Fixture(InvoiceProcessingStatus.SentToFinance);
        var unlocked = fixture.AddUnlockedItem();
        var values = fixture.Values() with
        {
            Items = [fixture.StoredValues(fixture.Item), fixture.StoredValues(unlocked) with { ReceiptNumber = "CHANGED" }]
        };
        var result = await fixture.Context.Sender.Send(new UpdateGoodsReceiptCommand(fixture.Receipt.Id,
            values, false, invoice, receipt, fixture.Receipt.ConcurrencyStamp, warehouse));
        Assert.True(result.IsSuccess, string.Join(" ", result.Messages));
        Assert.Equal(receipt ? "CHANGED" : "OPEN", unlocked.ReceiptNumber);
        Assert.Equal("REC-1", fixture.Item.ReceiptNumber);
    }

    [Fact]
    public async Task MixedReceipt_CannotAddAnotherItemToClosedInvoice()
    {
        await using var fixture = new Fixture(InvoiceProcessingStatus.SentToFinance);
        var locked = fixture.StoredValues(fixture.Item);
        var values = fixture.Values() with { PageNumber = "1", Items = [locked, locked with { Id = null, Quantity = 2 }] };
        var result = await fixture.Context.Sender.Send(new UpdateGoodsReceiptCommand(fixture.Receipt.Id,
            values, true, true, true, fixture.Receipt.ConcurrencyStamp));
        Assert.False(result.IsSuccess);
        Assert.Contains(Invoice.LockedMessage, result.Messages);
        await fixture.Context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(InvoiceProcessingStatus.SentToFinance)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved)]
    public async Task Create_SameNumberAndSupplierOnAnotherDay_UsesAnotherInvoice(InvoiceProcessingStatus status)
    {
        await using var fixture = new Fixture(status);
        var values = fixture.Values() with { EnteredAtUtc = fixture.Receipt.EnteredAtUtc.AddDays(1) };
        var result = await fixture.Context.Sender.Send(values);
        Assert.True(result.IsSuccess, string.Join(" ", result.Messages));
        var created = fixture.Receipts.Entities.Single(receipt => receipt.Id != fixture.Receipt.Id).Items.Single();
        Assert.NotEqual(fixture.Invoice.Id, created.InvoiceId);
        Assert.Equal(new DateOnly(2026, 10, 31), created.Invoice!.ReceiptDate);
        Assert.False(created.Invoice.IsLocked);
        Assert.Equal(status, fixture.Invoice.ProcessingStatus);
        Assert.Equal(1, fixture.Item.Quantity);
    }

    [Theory]
    [InlineData(InvoiceProcessingStatus.SentToFinance)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved)]
    [InlineData(InvoiceProcessingStatus.SentToFinance, 1)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved, 1)]
    public async Task Import_OnlySameDayIsBlockedByLockedInvoice(InvoiceProcessingStatus status, int dayDifference = 0)
    {
        await using var fixture = new Fixture(status);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("ورود کالا");
        sheet.Cell(1, 1).Value = "ردیف";
        sheet.Cell(2, 2).Value = dayDifference == 0 ? "2026/10/30" : "2026/10/31";
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
        Assert.Equal(dayDifference != 0, result.IsSuccess);
        if (dayDifference == 0)
        {
            Assert.Contains(result.Messages, message => message.Contains(Invoice.LockedMessage));
            await fixture.Context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
        }
        else
        {
            Assert.Empty(result.Data!.InvalidRows);
            Assert.Equal(1, result.Data.ImportedItemCount);
            var imported = fixture.Receipts.Entities.Single(receipt => receipt.Id != fixture.Receipt.Id).Items.Single();
            Assert.NotEqual(fixture.Invoice.Id, imported.InvoiceId);
            Assert.Equal(new DateOnly(2026, 10, 31), imported.Invoice!.ReceiptDate);
        }
    }

    [Theory]
    [InlineData(InvoiceProcessingStatus.SentToFinance, InvoiceProcessingStatus.UnderReview)]
    [InlineData(InvoiceProcessingStatus.SentToFinance, InvoiceProcessingStatus.SentToFinance)]
    [InlineData(InvoiceProcessingStatus.SentToFinance, InvoiceProcessingStatus.FinanceApproved)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved, InvoiceProcessingStatus.UnderReview)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved, InvoiceProcessingStatus.SentToFinance)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved, InvoiceProcessingStatus.FinanceApproved)]
    public async Task MixedReceipt_CanEditOrConfirmOpenInvoice_RegardlessOfOtherInvoicesStatus(
        InvoiceProcessingStatus closedStatus, InvoiceProcessingStatus targetStatus)
    {
        await using var fixture = new Fixture(closedStatus);
        var openItem = await fixture.AddOpenInvoiceItemAsync();
        var openInvoice = openItem.Invoice!;
        var stamp = fixture.Receipt.ConcurrencyStamp;
        var result = await fixture.Context.Sender.Send(new UpdateInvoiceCommand(openInvoice.Id,
            "INV-NEW-EDITED", fixture.Supplier.Id, "Edited open invoice", new DateOnly(2026, 10, 30), targetStatus,
            [new(openItem.Id, "OPEN-EDITED", stamp, 250)], FinanceDeliveryDate: new DateOnly(2026, 11, 3)));

        Assert.True(result.IsSuccess, string.Join(" ", result.Messages));
        Assert.Equal(targetStatus, openInvoice.ProcessingStatus);
        Assert.Equal("INV-NEW-EDITED", openInvoice.InvoiceNumber);
        Assert.Equal(250, openInvoice.Price);
        Assert.Equal("OPEN-EDITED", openItem.ReceiptNumber);
        Assert.Equal(250, openItem.Price);
        Assert.NotEqual(stamp, fixture.Receipt.ConcurrencyStamp);
        Assert.Equal(closedStatus, fixture.Invoice.ProcessingStatus);
        Assert.Equal("INV-1", fixture.Invoice.InvoiceNumber);
        Assert.Equal(100, fixture.Invoice.Price);
        Assert.Equal("REC-1", fixture.Item.ReceiptNumber);
        Assert.Null(fixture.Item.Price);
        Assert.Equal(new DateOnly(2026, 10, 30), fixture.Invoice.FinanceDeliveryDate);
        await fixture.Context.UnitOfWork.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(InvoiceProcessingStatus.SentToFinance)]
    [InlineData(InvoiceProcessingStatus.FinanceApproved)]
    public async Task MixedReceipt_CanEditOpenInvoiceItemReceipt_RegardlessOfOtherInvoicesStatus(InvoiceProcessingStatus closedStatus)
    {
        await using var fixture = new Fixture(closedStatus);
        var openItem = await fixture.AddOpenInvoiceItemAsync();
        var result = await fixture.Context.Sender.Send(new UpdateInvoiceItemCommand(openItem.InvoiceId!.Value,
            openItem.Id, "OPEN-EDITED", fixture.Receipt.ConcurrencyStamp));
        Assert.True(result.IsSuccess, string.Join(" ", result.Messages));
        Assert.Equal("OPEN-EDITED", openItem.ReceiptNumber);
        Assert.Equal(InvoiceProcessingStatus.UnderReview, openItem.Invoice!.ProcessingStatus);
        Assert.Equal("REC-1", fixture.Item.ReceiptNumber);
        Assert.Equal(closedStatus, fixture.Invoice.ProcessingStatus);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationTestContext Context { get; } = new();
        public Invoice Invoice { get; }
        public GoodsReceipt Receipt { get; }
        public GoodsReceiptItem Item => Receipt.Items.First();
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
            Context.UnitOfWork.When(work => work.SaveAsync(Arg.Any<CancellationToken>())).Do(_ =>
            {
                foreach (var receipt in Receipts.Entities)
                {
                    Set(receipt, nameof(GoodsReceipt.Deliverer), Person);
                    foreach (var item in receipt.Items)
                    {
                        Set(item, nameof(GoodsReceiptItem.Product), Product);
                        Set(item, nameof(GoodsReceiptItem.Warehouse), Warehouse);
                        if (item.Invoice is not null) Set(item.Invoice, nameof(Invoice.Supplier), Supplier);
                    }
                }
            });
        }
        public async Task<GoodsReceiptItem> AddOpenInvoiceItemAsync()
        {
            var invoice = new Invoice("INV-NEW", Supplier.Id, Invoice.ReceiptDate);
            Set(invoice, nameof(Invoice.Supplier), Supplier);
            await Context.UnitOfWork.Repository<Invoice>().AddAsync(invoice);
            var item = AddUnlockedItem();
            item.Update(item.WarehouseId, InvoiceStatus.HasInvoice, invoice.Id, invoice, item.ReceiptNumber,
                item.Quantity, item.RequestNumber, item.Description, item.SortOrder);
            invoice.GoodsReceiptItems.Add(item);
            return item;
        }
        public GoodsReceiptItem AddUnlockedItem()
        {
            Receipt.AddItem(Product.Id, Warehouse.Id, null, null, "OPEN", 2, null, null, 1);
            var added = Receipt.Items.Last();
            Set(added, nameof(GoodsReceiptItem.GoodsReceipt), Receipt);
            Set(added, nameof(GoodsReceiptItem.Product), Product);
            Set(added, nameof(GoodsReceiptItem.Warehouse), Warehouse);
            Register(Receipt.Items.ToList());
            return added;
        }
        public CreateGoodsReceiptItem StoredValues(GoodsReceiptItem item) => new(item.ProductId, null, null, null,
            item.WarehouseId, item.InvoiceStatus, item.Invoice?.InvoiceNumber, item.Invoice?.SupplierId, null,
            item.ReceiptNumber, item.Quantity, item.RequestNumber, item.Description, Id: item.Id);
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
