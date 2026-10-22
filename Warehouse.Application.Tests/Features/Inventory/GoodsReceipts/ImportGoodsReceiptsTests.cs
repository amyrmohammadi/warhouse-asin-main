using ClosedXML.Excel;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.ImportGoodsReceipts;
using Warehouse.Domain.Entities;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsReceipts;

public sealed class ImportGoodsReceiptsTests
{
    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("ندارد", InvoiceStatus.DoesNotHave)]
    [InlineData("دارد", InvoiceStatus.HasInvoice)]
    [InlineData("invalid", null)]
    public async Task Import_PreservesEmptyStatusAsNull_AndRejectsUnknownValues(string invoice, InvoiceStatus? expected)
    {
        await using var context = new ApplicationTestContext();
        var unit = new UnitOfMeasure("عدد", null);
        var warehouse = new StorageWarehouse("انبار", null);
        var product = new Product("کالا", "001", unit.Id, null);
        var products = Substitute.For<IRepositoryAsync<Product, Guid>>();
        var units = Substitute.For<IRepositoryAsync<UnitOfMeasure, Guid>>();
        var warehouses = Substitute.For<IRepositoryAsync<StorageWarehouse, Guid>>();
        var suppliers = Substitute.For<IRepositoryAsync<Supplier, Guid>>();
        var people = Substitute.For<IRepositoryAsync<Person, Guid>>();
        var receipts = Substitute.For<IRepositoryAsync<GoodsReceipt, Guid>>();
        context.UnitOfWork.Repository<Product>().Returns(products);
        context.UnitOfWork.Repository<UnitOfMeasure>().Returns(units);
        context.UnitOfWork.Repository<StorageWarehouse>().Returns(warehouses);
        context.UnitOfWork.Repository<Supplier>().Returns(suppliers);
        context.UnitOfWork.Repository<Person>().Returns(people);
        context.UnitOfWork.Repository<GoodsReceipt>().Returns(receipts);
        products.GetAllAsync(Arg.Any<CancellationToken>()).Returns([product]);
        units.GetAllAsync(Arg.Any<CancellationToken>()).Returns([unit]);
        warehouses.GetAllAsync(Arg.Any<CancellationToken>()).Returns([warehouse]);
        suppliers.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Supplier>());
        people.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Person>());

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("ورود کالا");
        sheet.Cell(1, 1).Value = "ردیف";
        sheet.Cell(2, 2).Value = "2026/10/19";
        sheet.Cell(2, 4).Value = product.Title;
        sheet.Cell(2, 5).Value = product.Code;
        sheet.Cell(2, 6).Value = 2;
        sheet.Cell(2, 7).Value = unit.Title;
        sheet.Cell(2, 9).Value = "تحویل دهنده";
        sheet.Cell(2, 10).Value = invoice;
        sheet.Cell(2, 11).Value = expected == InvoiceStatus.HasInvoice ? "INV-1" : "";
        sheet.Cell(2, 14).Value = warehouse.Title;
        sheet.Cell(2, 15).Value = "1";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var result = await context.Sender.Send(new ImportGoodsReceiptsCommand("receipts.xlsx", stream.ToArray(), true));

        Assert.True(result.IsSuccess);
        if (invoice == "invalid")
        {
            Assert.Single(result.Data!.InvalidRows);
            await receipts.DidNotReceive().AddAsync(Arg.Any<GoodsReceipt>(), Arg.Any<CancellationToken>());
            await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
        }
        else
        {
            Assert.Empty(result.Data!.InvalidRows);
            Assert.Equal(1, result.Data.ImportedReceiptCount);
            await receipts.Received(1).AddAsync(
                Arg.Is<GoodsReceipt>(r => r.Items.Single().InvoiceStatus == expected),
                Arg.Any<CancellationToken>());
            await context.UnitOfWork.Received(1).SaveAsync(Arg.Any<CancellationToken>());
        }
    }
}
