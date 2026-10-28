using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.Invoices.Commands.UpdateInvoice;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.Invoices;

public sealed class UpdateInvoiceTests
{
    [Fact]
    public async Task Update_SavesDetailsAndEachItemReceiptNumber()
    {
        await using var context = new ApplicationTestContext();
        var invoice = new Invoice("INV-1", null);
        var supplier = new Supplier("طرف حساب جدید", null);
        var unit = new UnitOfMeasure("عدد", null);
        var product = new Product("کالا", "P-1", unit.Id, null);
        var warehouse = new StorageWarehouse("انبار", null);
        var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, null, Guid.NewGuid(), "1");
        typeof(Product).GetProperty(nameof(Product.UnitOfMeasure))!.SetValue(product, unit);
        receipt.AddItem(product.Id, warehouse.Id, InvoiceStatus.HasInvoice, invoice,
            null, 1, null, null, 0);
        receipt.AddItem(product.Id, warehouse.Id, InvoiceStatus.HasInvoice, invoice,
            null, 2, null, null, 1);
        foreach (var item in receipt.Items)
        {
            invoice.GoodsReceiptItems.Add(item);
            typeof(GoodsReceiptItem).GetProperty(nameof(GoodsReceiptItem.GoodsReceipt))!.SetValue(item, receipt);
            typeof(GoodsReceiptItem).GetProperty(nameof(GoodsReceiptItem.Product))!.SetValue(item, product);
            typeof(GoodsReceiptItem).GetProperty(nameof(GoodsReceiptItem.Warehouse))!.SetValue(item, warehouse);
        }

        Register(context, new MemoryRepository<Invoice>([invoice]));
        Register(context, new MemoryRepository<Supplier>([supplier]));
        Register(context, new MemoryRepository<GoodsReceipt>([receipt]));
        Register(context, new MemoryRepository<GoodsReceiptItem>(receipt.Items.ToList()));
        var items = receipt.Items.ToArray();
        var result = await context.Sender.Send(new UpdateInvoiceCommand(invoice.Id,
            "  INV-2  ", supplier.Id, "  توضیحات جدید  ", new DateOnly(2026, 10, 27),
            InvoiceProcessingStatus.SentToFinance,
            [new(items[0].Id, "R-1", receipt.ConcurrencyStamp),
             new(items[1].Id, "R-2", receipt.ConcurrencyStamp)]));

        Assert.True(result.IsSuccess);
        Assert.Equal("INV-2", invoice.InvoiceNumber);
        Assert.Equal(supplier.Id, invoice.SupplierId);
        Assert.Equal("توضیحات جدید", invoice.Description);
        Assert.Equal(new DateOnly(2026, 10, 27), invoice.InvoiceDate);
        Assert.Equal(InvoiceProcessingStatus.SentToFinance, invoice.ProcessingStatus);
        Assert.Equal(["R-1", "R-2"], result.Data!.Items.Select(item => item.ReceiptNumber));
        await context.UnitOfWork.Received(1).SaveAsync(Arg.Any<CancellationToken>());
        await context.UnitOfWork.Received(1).CommitTransaction(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithStaleReceiptStamp_DoesNotSave()
    {
        await using var context = new ApplicationTestContext();
        var invoice = new Invoice("INV-1", null);
        var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, null, Guid.NewGuid(), "1");
        receipt.AddItem(Guid.NewGuid(), Guid.NewGuid(), InvoiceStatus.HasInvoice,
            invoice, null, 1, null, null, 0);
        var item = receipt.Items.Single();
        Register(context, new MemoryRepository<Invoice>([invoice]));
        Register(context, new MemoryRepository<GoodsReceipt>([receipt]));
        Register(context, new MemoryRepository<GoodsReceiptItem>([item]));
        var stale = Guid.NewGuid();
        while (stale == receipt.ConcurrencyStamp) stale = Guid.NewGuid();

        var result = await context.Sender.Send(new UpdateInvoiceCommand(invoice.Id,
            "INV-2", null, "نباید ذخیره شود", null, InvoiceProcessingStatus.FinanceApproved,
            [new(item.Id, "R-1", stale)]));

        Assert.False(result.IsSuccess);
        Assert.Contains(ConcurrencyMessages.Conflict, result.Messages);
        Assert.Null(invoice.Description);
        Assert.Null(item.ReceiptNumber);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithDuplicateInvoiceNumberForSameSupplier_DoesNotSave()
    {
        await using var context = new ApplicationTestContext();
        var supplier = new Supplier("تأمین‌کننده", null);
        var invoice = new Invoice("INV-1", supplier.Id);
        Register(context, new MemoryRepository<Invoice>([invoice, new Invoice("INV-2", supplier.Id)]));

        var result = await context.Sender.Send(new UpdateInvoiceCommand(invoice.Id,
            "INV-2", supplier.Id, null, null, InvoiceProcessingStatus.UnderReview, []));

        Assert.False(result.IsSuccess);
        Assert.Equal("INV-1", invoice.InvoiceNumber);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithSameInvoiceNumberForDifferentSupplier_Saves()
    {
        await using var context = new ApplicationTestContext();
        var firstSupplier = new Supplier("تأمین‌کننده اول", null);
        var secondSupplier = new Supplier("تأمین‌کننده دوم", null);
        var invoice = new Invoice("INV-1", firstSupplier.Id);
        Register(context, new MemoryRepository<Invoice>(
            [invoice, new Invoice("INV-2", secondSupplier.Id)]));
        Register(context, new MemoryRepository<Supplier>([firstSupplier, secondSupplier]));

        var result = await context.Sender.Send(new UpdateInvoiceCommand(invoice.Id,
            "INV-2", firstSupplier.Id, null, null, InvoiceProcessingStatus.UnderReview,
            [], CanEditInvoice: true, CanEditReceipt: false));

        Assert.True(result.IsSuccess);
        Assert.Equal("INV-2", invoice.InvoiceNumber);
        Assert.Equal(firstSupplier.Id, invoice.SupplierId);
        await context.UnitOfWork.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithUnknownSupplier_DoesNotSave()
    {
        await using var context = new ApplicationTestContext();
        var invoice = new Invoice("INV-1", null);
        Register(context, new MemoryRepository<Invoice>([invoice]));
        Register(context, new MemoryRepository<Supplier>([]));

        var result = await context.Sender.Send(new UpdateInvoiceCommand(invoice.Id,
            "INV-2", Guid.NewGuid(), null, null, InvoiceProcessingStatus.UnderReview, []));

        Assert.False(result.IsSuccess);
        Assert.Equal("INV-1", invoice.InvoiceNumber);
        Assert.Null(invoice.SupplierId);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Update_OnlyChangesTheAuthorizedInvoiceOrReceiptSection(
        bool canEditInvoice, bool canEditReceipt)
    {
        await using var context = new ApplicationTestContext();
        var invoice = new Invoice("INV-1", null);
        var unit = new UnitOfMeasure("عدد", null);
        var product = new Product("کالا", "P-1", unit.Id, null);
        var warehouse = new StorageWarehouse("انبار", null);
        var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, null, Guid.NewGuid(), "1");
        typeof(Product).GetProperty(nameof(Product.UnitOfMeasure))!.SetValue(product, unit);
        receipt.AddItem(product.Id, warehouse.Id, InvoiceStatus.HasInvoice, invoice,
            "R-OLD", 1, null, null, 0);
        var item = receipt.Items.Single();
        invoice.GoodsReceiptItems.Add(item);
        typeof(GoodsReceiptItem).GetProperty(nameof(GoodsReceiptItem.GoodsReceipt))!.SetValue(item, receipt);
        typeof(GoodsReceiptItem).GetProperty(nameof(GoodsReceiptItem.Product))!.SetValue(item, product);
        typeof(GoodsReceiptItem).GetProperty(nameof(GoodsReceiptItem.Warehouse))!.SetValue(item, warehouse);

        Register(context, new MemoryRepository<Invoice>([invoice]));
        Register(context, new MemoryRepository<Supplier>([]));
        Register(context, new MemoryRepository<GoodsReceipt>([receipt]));
        Register(context, new MemoryRepository<GoodsReceiptItem>([item]));

        var result = await context.Sender.Send(new UpdateInvoiceCommand(invoice.Id,
            "INV-2", null, "شرح جدید", null, InvoiceProcessingStatus.FinanceApproved,
            [new(item.Id, "R-NEW", receipt.ConcurrencyStamp)], canEditInvoice, canEditReceipt));

        Assert.True(result.IsSuccess);
        Assert.Equal(canEditInvoice ? "INV-2" : "INV-1", invoice.InvoiceNumber);
        Assert.Equal(canEditInvoice ? "شرح جدید" : null, invoice.Description);
        Assert.Equal(canEditReceipt ? "R-NEW" : "R-OLD", item.ReceiptNumber);
    }

    private static void Register<T>(ApplicationTestContext context, MemoryRepository<T> repository)
        where T : Entity<Guid> => context.UnitOfWork.Repository<T>().Returns(repository);

    private sealed class MemoryRepository<T>(List<T> data) : IRepositoryAsync<T, Guid>
        where T : Entity<Guid>
    {
        public IQueryable<T> Entities => data.AsQueryable();
        public Task<T?> GetByIdAsync(Guid id, CancellationToken token = default) =>
            Task.FromResult(data.FirstOrDefault(item => item.Id == id));
        public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken token = default) =>
            Task.FromResult<IReadOnlyList<T>>(data);
        public Task<IReadOnlyList<T>> GetPagedResponseAsync(int page, int size, CancellationToken token = default) =>
            Task.FromResult<IReadOnlyList<T>>(data.Skip((page - 1) * size).Take(size).ToList());
        public Task<int> CountAsync(IQueryable<T> query, CancellationToken token = default) =>
            Task.FromResult(query.Count());
        public Task<IReadOnlyList<T>> ToListAsync(IQueryable<T> query, CancellationToken token = default) =>
            Task.FromResult<IReadOnlyList<T>>(query.ToList());
        public Task<IReadOnlyList<TResult>> ToListAsync<TResult>(
            IQueryable<TResult> query, CancellationToken token = default) =>
            Task.FromResult<IReadOnlyList<TResult>>(query.ToList());
        public Task<T> AddAsync(T entity, CancellationToken token = default) => Task.FromResult(entity);
        public Task UpdateAsync(T entity, CancellationToken token = default) => Task.CompletedTask;
        public Task DeleteAsync(T entity, CancellationToken token = default) => Task.CompletedTask;
    }
}
