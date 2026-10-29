using Warehouse.Application.Common.Concurrency;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.Invoices.Commands.UpdateInvoice;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.Invoices;

public sealed class UpdateInvoiceTests
{
    [Theory]
    [InlineData(null, 225)]
    [InlineData(500, 500)]
    [InlineData(0, 0)]
    public async Task Update_SavesPrices_AndUsesCalculatedOrManualInvoiceTotal(int? manualPrice, int expectedPrice)
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
            [new(items[0].Id, "R-1", receipt.ConcurrencyStamp, 100),
             new(items[1].Id, "R-2", receipt.ConcurrencyStamp, 125)], Price: manualPrice,
             FinanceDeliveryDate: new DateOnly(2026, 10, 29)));

        Assert.True(result.IsSuccess);
        Assert.Equal("INV-2", invoice.InvoiceNumber);
        Assert.Equal(supplier.Id, invoice.SupplierId);
        Assert.Equal("توضیحات جدید", invoice.Description);
        Assert.Equal(new DateOnly(2026, 10, 27), invoice.InvoiceDate);
        Assert.Equal(new DateOnly(2026, 10, 29), invoice.FinanceDeliveryDate);
        Assert.Equal(invoice.FinanceDeliveryDate, result.Data!.FinanceDeliveryDate);
        Assert.Equal(InvoiceProcessingStatus.SentToFinance, invoice.ProcessingStatus);
        Assert.Equal(["R-1", "R-2"], result.Data!.Items.Select(item => item.ReceiptNumber));
        Assert.Equal(expectedPrice, invoice.Price);
        Assert.Equal(invoice.Price, result.Data.Price);
        Assert.Equal(new decimal?[] { 100, 125 }, result.Data.Items.Select(item => item.Price));
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
        Register(context, new MemoryRepository<GoodsReceiptItem>([]));
        Register(context, new MemoryRepository<Invoice>(
            [invoice, new Invoice("INV-2", secondSupplier.Id)]));
        Register(context, new MemoryRepository<Supplier>([firstSupplier, secondSupplier]));

        var result = await context.Sender.Send(new UpdateInvoiceCommand(invoice.Id,
            "INV-2", firstSupplier.Id, null, null, InvoiceProcessingStatus.UnderReview,
            [], CanEditInvoice: true, CanEditReceipt: false, Price: 900));

        Assert.True(result.IsSuccess);
        Assert.Equal("INV-2", invoice.InvoiceNumber);
        Assert.Equal(firstSupplier.Id, invoice.SupplierId);
        Assert.Equal(900, invoice.Price);
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
        item.UpdatePrice(10);
        invoice.UpdatePrice(10);
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
            [new(item.Id, "R-NEW", receipt.ConcurrencyStamp, 25)], canEditInvoice, canEditReceipt, Price: 50));

        Assert.True(result.IsSuccess);
        Assert.Equal(canEditInvoice ? "INV-2" : "INV-1", invoice.InvoiceNumber);
        Assert.Equal(canEditInvoice ? "شرح جدید" : null, invoice.Description);
        Assert.Equal(canEditReceipt ? "R-NEW" : "R-OLD", item.ReceiptNumber);
        Assert.Equal(canEditInvoice ? 25 : 10, item.Price);
        Assert.Equal(canEditInvoice ? 50 : 10, invoice.Price);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0.001)]
    [InlineData(10000000000000000)]
    public async Task Update_WithInvalidPrice_IsRejected(double invalidPrice)
    {
        await using var context = new ApplicationTestContext();
        var command = new UpdateInvoiceCommand(Guid.NewGuid(), "INV-1", null, null, null,
            InvoiceProcessingStatus.UnderReview, [], Price: (decimal)invalidPrice);
        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(command));
        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(command with
        {
            Price = null,
            Items = [new(Guid.NewGuid(), null, Guid.NewGuid(), (decimal)invalidPrice)]
        }));
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
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
