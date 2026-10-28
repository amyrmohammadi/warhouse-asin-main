using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptItemRows;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceipts;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsReceipts;

public sealed class GetGoodsReceiptSearchTests
{
    [Theory]
    [InlineData("شرح سند ویژه")]
    [InlineData("شرح ردیف ویژه")]
    [InlineData("تأمین‌کننده ویژه")]
    public async Task Search_MatchesDescriptionsAndSupplierTitle_InBothTableModes(string search)
    {
        await using var context = new ApplicationTestContext();
        var matching = CreateReceipt(
            "شرح سند ویژه",
            "شرح ردیف ویژه",
            "تأمین‌کننده ویژه",
            "MATCH");
        var unrelated = CreateReceipt(
            "شرح سند دیگر",
            "شرح ردیف دیگر",
            "تأمین‌کننده دیگر",
            "OTHER");

        var receiptRepository = ReceiptRepository([matching, unrelated]);
        var itemRepository = ItemRepository([.. matching.Items, .. unrelated.Items]);
        context.UnitOfWork.Repository<GoodsReceipt>().Returns(receiptRepository);
        context.UnitOfWork.Repository<GoodsReceiptItem>().Returns(itemRepository);

        var documents = await context.Sender.Send(new GetGoodsReceiptsQuery { Search = search });
        var items = await context.Sender.Send(new GetGoodsReceiptItemRowsQuery { Search = search });

        Assert.Equal(1, documents.Data!.FilteredCount);
        Assert.Equal(1, items.Data!.FilteredCount);
    }

    private static GoodsReceipt CreateReceipt(
        string receiptDescription,
        string itemDescription,
        string supplierTitle,
        string suffix)
    {
        var deliverer = new Person($"تحویل‌دهنده {suffix}", string.Empty);
        var receipt = new GoodsReceipt(
            DateTimeOffset.UtcNow,
            receiptDescription,
            deliverer.Id,
            $"PAGE-{suffix}");
        Set(receipt, nameof(GoodsReceipt.Deliverer), deliverer);

        var supplier = new Supplier(supplierTitle, null);
        var invoice = new Invoice($"INV-{suffix}", supplier.Id);
        Set(invoice, nameof(Invoice.Supplier), supplier);
        var product = new Product($"کالا {suffix}", $"P-{suffix}", Guid.NewGuid(), null);
        var warehouse = new StorageWarehouse($"انبار {suffix}", null);

        receipt.AddItem(
            product.Id,
            warehouse.Id,
            InvoiceStatus.HasInvoice,
            invoice,
            $"R-{suffix}",
            1,
            null,
            itemDescription,
            0);
        var item = receipt.Items.Single();
        Set(item, nameof(GoodsReceiptItem.GoodsReceipt), receipt);
        Set(item, nameof(GoodsReceiptItem.Product), product);
        Set(item, nameof(GoodsReceiptItem.Warehouse), warehouse);
        return receipt;
    }

    private static void Set(object target, string propertyName, object value) =>
        target.GetType().GetProperty(propertyName)!.SetValue(target, value);

    private static IRepositoryAsync<GoodsReceipt, Guid> ReceiptRepository(GoodsReceipt[] values)
    {
        var repository = Substitute.For<IRepositoryAsync<GoodsReceipt, Guid>>();
        repository.Entities.Returns(values.AsQueryable());
        repository.CountAsync(Arg.Any<IQueryable<GoodsReceipt>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<GoodsReceipt>>().Count());
        repository.ToListAsync(Arg.Any<IQueryable<GoodsReceiptSummary>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        return repository;
    }

    private static IRepositoryAsync<GoodsReceiptItem, Guid> ItemRepository(GoodsReceiptItem[] values)
    {
        var repository = Substitute.For<IRepositoryAsync<GoodsReceiptItem, Guid>>();
        repository.Entities.Returns(values.AsQueryable());
        repository.CountAsync(Arg.Any<IQueryable<GoodsReceiptItem>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<GoodsReceiptItem>>().Count());
        repository.ToListAsync(
                Arg.Any<IQueryable<GoodsReceiptItemRowSummary>>(),
                Arg.Any<CancellationToken>())
            .Returns([]);
        return repository;
    }
}
