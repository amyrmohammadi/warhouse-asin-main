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
    public async Task Update_OnlyChangesAuthorizedSections_AndPreservesIds(
        bool initial, bool invoice, InvoiceStatus? status)
    {
        await using var context = new ApplicationTestContext();
        var warehouse = new StorageWarehouse("انبار", null);
        var person = new Person("تحویل دهنده", "");
        var unit = new UnitOfMeasure("عدد", null);
        var product = new Product("کالا", "001", unit.Id, null);
        typeof(Product).GetProperty(nameof(Product.UnitOfMeasure))!.SetValue(product, unit);

        var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, "old", person.Id, "1");
        typeof(GoodsReceipt).GetProperty(nameof(GoodsReceipt.Deliverer))!.SetValue(receipt, person);
        receipt.AddItem(product.Id, warehouse.Id, InvoiceStatus.HasInvoice, "OLD-INVOICE",
            null, "OLD-RECEIPT", 2, "old-request", "old-description", 0);
        var item = receipt.Items.Single();
        typeof(GoodsReceiptItem).GetProperty(nameof(GoodsReceiptItem.Product))!.SetValue(item, product);
        typeof(GoodsReceiptItem).GetProperty(nameof(GoodsReceiptItem.Warehouse))!.SetValue(item, warehouse);
        var itemId = item.Id;

        Register(context, new MemoryRepository<StorageWarehouse>([warehouse]));
        Register(context, new MemoryRepository<Person>([person]));
        Register(context, new MemoryRepository<Product>([product]));
        Register(context, new MemoryRepository<UnitOfMeasure>([unit]));
        Register(context, new MemoryRepository<Supplier>([]));
        Register(context, new MemoryRepository<GoodsReceipt>([receipt]));
        Register(context, new MemoryRepository<GoodsReceiptItem>([item]));

        var values = new CreateGoodsReceiptCommand(
            receipt.EnteredAtUtc.AddDays(1), "new", person.Id, null, "2",
            [new CreateGoodsReceiptItem(
                product.Id, null, null, null, warehouse.Id, status,
                status.HasValue ? "NEW-INVOICE" : null, null, null, "NEW-RECEIPT",
                7, "new-request", "new-description")]);

        var result = await context.Sender.Send(
            new UpdateGoodsReceiptCommand(receipt.Id, values, initial, invoice));

        Assert.True(result.IsSuccess);
        Assert.Equal(receipt.Id, result.Data!.Id);
        Assert.Equal(initial ? "new" : "old", receipt.Description);
        Assert.Equal(initial ? "2" : "1", receipt.PageNumber);
        Assert.Equal(initial ? 7 : 2, item.Quantity);
        Assert.Equal(itemId, item.Id);
        Assert.Equal(invoice ? status : InvoiceStatus.HasInvoice, item.InvoiceStatus);
        Assert.Equal(invoice ? (status.HasValue ? "NEW-INVOICE" : null) : "OLD-INVOICE",
            item.InvoiceNumber);
        Assert.Equal(invoice ? "NEW-RECEIPT" : "OLD-RECEIPT", item.ReceiptNumber);
        Assert.Equal(item.InvoiceStatus, result.Data.Items.Single().InvoiceStatus);
        await context.UnitOfWork.Received(1).SaveAsync(Arg.Any<CancellationToken>());
        await context.UnitOfWork.Received(1).CommitTransaction(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateInitialEntry_PreservesRequestedItemOrder()
    {
        await using var context = new ApplicationTestContext();
        var warehouse = new StorageWarehouse("Warehouse", null);
        var person = new Person("Deliverer", "");
        var unit = new UnitOfMeasure("Unit", null);
        var firstProduct = new Product("First", "001", unit.Id, null);
        var secondProduct = new Product("Second", "002", unit.Id, null);
        typeof(Product).GetProperty(nameof(Product.UnitOfMeasure))!.SetValue(firstProduct, unit);
        typeof(Product).GetProperty(nameof(Product.UnitOfMeasure))!.SetValue(secondProduct, unit);

        var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, null, person.Id, "1");
        typeof(GoodsReceipt).GetProperty(nameof(GoodsReceipt.Deliverer))!.SetValue(receipt, person);
        receipt.AddItem(firstProduct.Id, warehouse.Id, null, null, null, null, 1, null, null, 0);
        receipt.AddItem(secondProduct.Id, warehouse.Id, null, null, null, null, 1, null, null, 1);
        var storedItems = receipt.Items.ToArray();
        typeof(GoodsReceiptItem).GetProperty(nameof(GoodsReceiptItem.Product))!
            .SetValue(storedItems[0], firstProduct);
        typeof(GoodsReceiptItem).GetProperty(nameof(GoodsReceiptItem.Product))!
            .SetValue(storedItems[1], secondProduct);
        foreach (var item in storedItems)
            typeof(GoodsReceiptItem).GetProperty(nameof(GoodsReceiptItem.Warehouse))!
                .SetValue(item, warehouse);

        Register(context, new MemoryRepository<StorageWarehouse>([warehouse]));
        Register(context, new MemoryRepository<Person>([person]));
        Register(context, new MemoryRepository<Product>([firstProduct, secondProduct]));
        Register(context, new MemoryRepository<UnitOfMeasure>([unit]));
        Register(context, new MemoryRepository<Supplier>([]));
        Register(context, new MemoryRepository<GoodsReceipt>([receipt]));
        Register(context, new MemoryRepository<GoodsReceiptItem>(storedItems.ToList()));

        CreateGoodsReceiptItem CreateItem(Product product) => new(
            product.Id, null, null, null, warehouse.Id, null, null, null, null, null,
            1, null, null);
        var values = new CreateGoodsReceiptCommand(
            receipt.EnteredAtUtc, null, person.Id, null, "1",
            [CreateItem(secondProduct), CreateItem(firstProduct)]);

        var result = await context.Sender.Send(
            new UpdateGoodsReceiptCommand(receipt.Id, values, true, true));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [secondProduct.Id, firstProduct.Id],
            result.Data!.Items.Select(item => item.ProductId));
        Assert.Equal(1, storedItems.Single(item => item.ProductId == firstProduct.Id).SortOrder);
        Assert.Equal(0, storedItems.Single(item => item.ProductId == secondProduct.Id).SortOrder);
    }

    [Fact]
    public async Task Update_UpdatesMultipleRowsForSameProduct_Independently()
    {
        await using var context = new ApplicationTestContext();
        var warehouse = new StorageWarehouse("Warehouse", null);
        var person = new Person("Deliverer", "");
        var unit = new UnitOfMeasure("Unit", null);
        var product = new Product("Product", "001", unit.Id, null);
        typeof(Product).GetProperty(nameof(Product.UnitOfMeasure))!.SetValue(product, unit);

        var receipt = new GoodsReceipt(DateTimeOffset.UtcNow, null, person.Id, "1");
        typeof(GoodsReceipt).GetProperty(nameof(GoodsReceipt.Deliverer))!.SetValue(receipt, person);
        receipt.AddItem(product.Id, warehouse.Id, null, null, null, null, 1, null, null, 0);
        receipt.AddItem(product.Id, warehouse.Id, null, null, null, null, 2, null, null, 1);
        var storedItems = receipt.Items.ToList();
        var storedIds = storedItems.Select(item => item.Id).ToArray();
        foreach (var storedItem in storedItems)
        {
            typeof(GoodsReceiptItem).GetProperty(nameof(GoodsReceiptItem.Product))!
                .SetValue(storedItem, product);
            typeof(GoodsReceiptItem).GetProperty(nameof(GoodsReceiptItem.Warehouse))!
                .SetValue(storedItem, warehouse);
        }

        Register(context, new MemoryRepository<StorageWarehouse>([warehouse]));
        Register(context, new MemoryRepository<Person>([person]));
        Register(context, new MemoryRepository<Product>([product]));
        Register(context, new MemoryRepository<UnitOfMeasure>([unit]));
        Register(context, new MemoryRepository<Supplier>([]));
        Register(context, new MemoryRepository<GoodsReceipt>([receipt]));
        Register(context, new MemoryRepository<GoodsReceiptItem>(storedItems));

        CreateGoodsReceiptItem Item(decimal quantity) => new(
            product.Id, null, null, null, warehouse.Id, null, null, null, null, null,
            quantity, null, null);
        var values = new CreateGoodsReceiptCommand(
            receipt.EnteredAtUtc, null, person.Id, null, "1", [Item(3), Item(4)]);

        var result = await context.Sender.Send(
            new UpdateGoodsReceiptCommand(receipt.Id, values, true, true));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Items.Count);
        Assert.Equal([3m, 4m], result.Data.Items.Select(item => item.Quantity));
        Assert.Equal(storedIds, storedItems.Select(item => item.Id));
        Assert.Equal(2, storedItems.Count);
    }

    [Fact]
    public async Task Update_WithoutPermissions_DoesNotWrite()
    {
        await using var context = new ApplicationTestContext();
        var result = await context.Sender.Send(
            new UpdateGoodsReceiptCommand(Guid.NewGuid(), null!, false, false));
        Assert.False(result.IsSuccess);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    private static void Register<T>(
        ApplicationTestContext context, MemoryRepository<T> repository) where T : Entity<Guid> =>
        context.UnitOfWork.Repository<T>().Returns(repository);

    private sealed class MemoryRepository<T>(List<T> data)
        : IRepositoryAsync<T, Guid> where T : Entity<Guid>
    {
        public IQueryable<T> Entities => data.AsQueryable();
        public Task<T?> GetByIdAsync(Guid id, CancellationToken token = default) =>
            Task.FromResult(data.FirstOrDefault(x => x.Id == id));
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
        public Task<T> AddAsync(T entity, CancellationToken token = default)
        {
            data.Add(entity);
            return Task.FromResult(entity);
        }
        public Task UpdateAsync(T entity, CancellationToken token = default) => Task.CompletedTask;
        public Task DeleteAsync(T entity, CancellationToken token = default)
        {
            data.Remove(entity);
            return Task.CompletedTask;
        }
    }
}
