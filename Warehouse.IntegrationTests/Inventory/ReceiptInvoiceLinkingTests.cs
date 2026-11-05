using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.ImportGoodsReceipts;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.UpdateGoodsReceipt;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;
using Warehouse.Infrastructure.Persistence;
using Warehouse.IntegrationTests.Infrastructure;

namespace Warehouse.IntegrationTests.Inventory;

public sealed class ReceiptInvoiceLinkingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task DeleteInvoice_RechecksActualItemsAndStatusBeforeDeleting(bool hasItems, bool locked)
    {
        using var factory = new FullStackWebApplicationFactory();
        await factory.InitializeDatabaseAsync();
        var seed = await SeedAsync(factory, standalone: !hasItems, keepDetails: true, locked: locked);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.Send(new Warehouse.Application.Features.Inventory.Invoices.Commands.DeleteInvoice.DeleteInvoiceCommand(seed.InvoiceId));
            Assert.Equal(!hasItems && !locked, result.IsSuccess);
        }
        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(hasItems || locked, await db.Set<Invoice>().AnyAsync(invoice => invoice.Id == seed.InvoiceId));
        Assert.Equal(hasItems ? 1 : 0, await db.Set<GoodsReceiptItem>().CountAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task EntryAndImport_ReuseStandaloneInvoice_AndRespectItsFinanceLock(bool import, bool locked)
    {
        using var factory = new FullStackWebApplicationFactory();
        await factory.InitializeDatabaseAsync();
        var seed = await SeedAsync(factory, standalone: true, keepDetails: true, locked: locked);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            bool success;
            if (import)
            {
                using var workbook = new XLWorkbook();
                var sheet = workbook.AddWorksheet("ورود");
                sheet.Cell(1, 1).Value = "ردیف";
                sheet.Cell(2, 2).Value = "2026/10/19";
                sheet.Cell(2, 4).Value = "Product";
                sheet.Cell(2, 5).Value = "P-1";
                sheet.Cell(2, 6).Value = 1;
                sheet.Cell(2, 7).Value = "Unit";
                sheet.Cell(2, 9).Value = "Person";
                sheet.Cell(2, 10).Value = "دارد";
                sheet.Cell(2, 11).Value = "INV-1";
                sheet.Cell(2, 12).Value = "Supplier";
                sheet.Cell(2, 14).Value = "Warehouse";
                sheet.Cell(2, 15).Value = "PAGE-1";
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var result = await sender.Send(new ImportGoodsReceiptsCommand("test.xlsx", stream.ToArray(), true, true));
                success = result.IsSuccess && result.Data!.ImportedItemCount == 1;
            }
            else
            {
                var result = await sender.Send(Values(seed, new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero)));
                success = result.IsSuccess;
            }
            Assert.Equal(!locked, success);
        }
        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var invoice = Assert.Single(await db.Set<Invoice>().ToListAsync());
        Assert.Equal(seed.InvoiceId, invoice.Id);
        Assert.Equal(1200, invoice.Price);
        Assert.Equal("Financial details", invoice.Description);
        Assert.Equal(locked ? InvoiceProcessingStatus.SentToFinance : InvoiceProcessingStatus.UnderReview, invoice.ProcessingStatus);
        if (locked)
        {
            Assert.Null(invoice.ReceiptDate);
            Assert.Empty(await db.Set<GoodsReceiptItem>().ToListAsync());
        }
        else
        {
            Assert.Equal(new DateOnly(2026, 10, 19), invoice.ReceiptDate);
            Assert.Equal(invoice.Id, Assert.Single(await db.Set<GoodsReceiptItem>().ToListAsync()).InvoiceId);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ChangingEntryDay_RemovesOnlyUnusedShell_AndPreservesFinancialDataAndOtherReceipts(bool keepDetails, bool otherReceipt)
    {
        using var factory = new FullStackWebApplicationFactory();
        await factory.InitializeDatabaseAsync();
        var seed = await SeedAsync(factory, standalone: false, keepDetails: keepDetails, otherReceipt: otherReceipt);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var receipt = await db.Set<GoodsReceipt>().SingleAsync(value => value.Id == seed.ReceiptId);
            var item = await db.Set<GoodsReceiptItem>().SingleAsync(value => value.GoodsReceiptId == receipt.Id);
            var values = Values(seed, receipt.EnteredAtUtc.AddDays(1));
            values = values with { Items = [values.Items.Single() with { Id = item.Id }] };
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new UpdateGoodsReceiptCommand(
                receipt.Id, values, true, true, true, receipt.ConcurrencyStamp));
            Assert.True(result.IsSuccess, string.Join("; ", result.Messages));
        }
        await using var check = factory.Services.CreateAsyncScope();
        var database = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var invoices = await database.Set<Invoice>().ToListAsync();
        Assert.Equal(keepDetails || otherReceipt ? 2 : 1, invoices.Count);
        var linked = await database.Set<GoodsReceiptItem>().SingleAsync(item => item.GoodsReceiptId == seed.ReceiptId);
        Assert.NotEqual(seed.InvoiceId, linked.InvoiceId);
        if (keepDetails) Assert.Equal(1200, invoices.Single(invoice => invoice.Id == seed.InvoiceId).Price);
        if (otherReceipt) Assert.Single(await database.Set<GoodsReceiptItem>().Where(item => item.InvoiceId == seed.InvoiceId).ToListAsync());
    }

    private static CreateGoodsReceiptCommand Values(Seed seed, DateTimeOffset date) => new(date, null,
        seed.PersonId, null, "PAGE-1", [new CreateGoodsReceiptItem(seed.ProductId, null, null, null,
            seed.WarehouseId, InvoiceStatus.HasInvoice, "INV-1", seed.SupplierId, null, null, 1, null, null)]);

    private static async Task<Seed> SeedAsync(FullStackWebApplicationFactory factory, bool standalone, bool keepDetails,
        bool locked = false, bool otherReceipt = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var unit = new UnitOfMeasure("Unit", null);
        var product = new Product("Product", "P-1", unit.Id, null);
        var warehouse = new StorageWarehouse("Warehouse", null);
        var person = new Person("Person", "");
        var supplier = new Supplier("Supplier", null);
        var date = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);
        var invoice = new Invoice("INV-1", supplier.Id, standalone ? null : Invoice.GetReceiptDate(date));
        if (keepDetails) { invoice.UpdatePrice(1200); invoice.UpdateDetails("Financial details", null, InvoiceProcessingStatus.UnderReview); }
        if (locked) invoice.UpdateDetails(invoice.Description, null, InvoiceProcessingStatus.SentToFinance);
        db.AddRange(unit, product, warehouse, person, supplier, invoice);
        Guid? receiptId = null;
        if (!standalone)
        {
            var receipt = new GoodsReceipt(date, null, person.Id, "PAGE-1");
            receipt.AddItem(product.Id, warehouse.Id, InvoiceStatus.HasInvoice, invoice, null, 1, null, null, 0);
            db.Add(receipt);
            receiptId = receipt.Id;
            if (otherReceipt)
            {
                var other = new GoodsReceipt(date, null, person.Id, "PAGE-2");
                other.AddItem(product.Id, warehouse.Id, InvoiceStatus.HasInvoice, invoice, null, 2, null, null, 0);
                db.Add(other);
            }
        }
        await db.SaveChangesAsync();
        return new Seed(product.Id, warehouse.Id, person.Id, supplier.Id, invoice.Id, receiptId);
    }

    private sealed record Seed(Guid ProductId, Guid WarehouseId, Guid PersonId, Guid SupplierId, Guid InvoiceId, Guid? ReceiptId);
}
