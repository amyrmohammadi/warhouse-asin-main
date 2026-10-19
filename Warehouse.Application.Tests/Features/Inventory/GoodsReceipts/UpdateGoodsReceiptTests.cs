using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.UpdateGoodsReceipt;
using Warehouse.Domain.Common;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsReceipts;

public sealed class UpdateGoodsReceiptTests
{
    [Theory]
    [InlineData(true, false, InvoiceStatus.HasInvoice)]
    [InlineData(false, true, InvoiceStatus.HasInvoice)]
    [InlineData(true, true, InvoiceStatus.HasInvoice)]
    [InlineData(false, true, null)]
    [InlineData(true, false, null)]
    public async Task Update_OnlyChangesAuthorizedSections_AndPreservesReceiptAndItemIds(bool initial, bool invoice, InvoiceStatus? status)
    {
        await using var context = new ApplicationTestContext();
        var warehouse = new StorageWarehouse("انبار", null);
        var person = new Person("تحویل دهنده", "");
        var unit = new UnitOfMeasure("عدد", null);
        var product = new Product("کالا", "001", unit.Id, null);
        typeof(Product).GetProperty(nameof(Product.UnitOfMeasure))!.SetValue(product, unit);
        var removedProduct = new Product("حذف از سند", "002", unit.Id, null);
        var addedProduct = new Product("افزودن به سند", "003", unit.Id, null);
        typeof(Product).GetProperty(nameof(Product.UnitOfMeasure))!.SetValue(removedProduct, unit);
        typeof(Product).GetProperty(nameof(Product.UnitOfMeasure))!.SetValue(addedProduct, unit);
        var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, "old", person.Id, warehouse.Id,
            InvoiceStatus.HasInvoice, "OLD-INVOICE", null, "OLD-RECEIPT", "1");
        receipt.AddItem(product.Id, 2, "old-request", "old-description");
        var item = receipt.Items.Single();
        var itemId = item.Id;
        receipt.AddItem(removedProduct.Id, 3, null, null);
        var removedItem = receipt.Items.Last();
        Register(context, new MemoryRepository<StorageWarehouse>([warehouse]));
        Register(context, new MemoryRepository<Person>([person]));
        Register(context, new MemoryRepository<Product>([product, removedProduct, addedProduct]));
        Register(context, new MemoryRepository<UnitOfMeasure>([unit]));
        Register(context, new MemoryRepository<Supplier>([]));
        Register(context, new MemoryRepository<GoodsReceipt>([receipt]));
        var itemRepo = new MemoryRepository<GoodsReceiptItem>([item, removedItem]);
        Register(context, itemRepo);
        var values = new CreateGoodsReceiptCommand(receipt.EnteredAtUtc.AddDays(1), "new", person.Id, null,
            warehouse.Id, status, status.HasValue ? "NEW-INVOICE" : null, null, null, "NEW-RECEIPT", "2",
            [new CreateGoodsReceiptItem(product.Id, null, null, null, 7, "new-request", "new-description"),
             new CreateGoodsReceiptItem(addedProduct.Id, null, null, null, 4, null, null)]);
        var result = await context.Sender.Send(new UpdateGoodsReceiptCommand(receipt.Id, values, initial, invoice));
        Assert.True(result.IsSuccess);
        Assert.Equal(receipt.Id, result.Data!.Id);
        Assert.Equal(initial ? "new" : "old", receipt.Description);
        Assert.Equal(initial ? "2" : "1", receipt.PageNumber);
        Assert.Equal(initial ? 7 : 2, item.Quantity);
        Assert.Equal(itemId, item.Id);
        Assert.Equal(invoice ? status : InvoiceStatus.HasInvoice, receipt.InvoiceStatus);
        Assert.Equal(receipt.InvoiceStatus, result.Data.InvoiceStatus);
        Assert.Equal(invoice ? (status.HasValue ? "NEW-INVOICE" : null) : "OLD-INVOICE", receipt.InvoiceNumber);
        Assert.Equal(invoice ? "NEW-RECEIPT" : "OLD-RECEIPT", receipt.ReceiptNumber);
        Assert.Equal(initial ? 1 : 0, itemRepo.Deleted.Count);
        Assert.Equal(initial, itemRepo.Entities.Any(i => i.ProductId == addedProduct.Id));
        Assert.Equal(!initial, itemRepo.Entities.Any(i => i.ProductId == removedProduct.Id));
        await context.UnitOfWork.Received(1).SaveAsync(Arg.Any<CancellationToken>());
        await context.UnitOfWork.Received(1).CommitTransaction(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithoutPermissions_DoesNotWrite()
    {
        await using var context = new ApplicationTestContext();
        var result = await context.Sender.Send(new UpdateGoodsReceiptCommand(Guid.NewGuid(), null!, false, false));
        Assert.False(result.IsSuccess);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    private static void Register<T>(ApplicationTestContext context, MemoryRepository<T> repository) where T : Entity<Guid>
        => context.UnitOfWork.Repository<T>().Returns(repository);

    private sealed class MemoryRepository<T>(List<T> data) : IRepositoryAsync<T, Guid> where T : Entity<Guid>
    {
        public List<T> Deleted { get; } = [];
        public IQueryable<T> Entities => data.AsQueryable();
        public Task<T?> GetByIdAsync(Guid id, CancellationToken token = default) => Task.FromResult(data.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken token = default) => Task.FromResult<IReadOnlyList<T>>(data);
        public Task<IReadOnlyList<T>> GetPagedResponseAsync(int page, int size, CancellationToken token = default) => Task.FromResult<IReadOnlyList<T>>(data.Skip((page - 1) * size).Take(size).ToList());
        public Task<int> CountAsync(IQueryable<T> query, CancellationToken token = default) => Task.FromResult(query.Count());
        public Task<IReadOnlyList<T>> ToListAsync(IQueryable<T> query, CancellationToken token = default) => Task.FromResult<IReadOnlyList<T>>(query.ToList());
        public Task<IReadOnlyList<TResult>> ToListAsync<TResult>(IQueryable<TResult> query, CancellationToken token = default) => Task.FromResult<IReadOnlyList<TResult>>(query.ToList());
        public Task<T> AddAsync(T entity, CancellationToken token = default) { data.Add(entity); return Task.FromResult(entity); }
        public Task UpdateAsync(T entity, CancellationToken token = default) => Task.CompletedTask;
        public Task DeleteAsync(T entity, CancellationToken token = default) { data.Remove(entity); Deleted.Add(entity); return Task.CompletedTask; }
    }
}
