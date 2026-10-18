using ClosedXML.Excel;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.MasterData.Suppliers.Commands.ImportSuppliers;
using Warehouse.Application.Tests.TestSupport;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Tests.Features.MasterData.Suppliers;

public sealed class ImportSuppliersFeatureTests
{
    [Fact]
    public async Task Import_ReadsColumnB_AndSkipsHeadersDuplicatesAndInvalidRows()
    {
        await using var context = new ApplicationTestContext();
        var repository = Substitute.For<IRepositoryAsync<Supplier, Guid>>();
        context.UnitOfWork.Repository<Supplier>().Returns(repository);
        repository
            .GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([new Supplier("تأمین‌کننده موجود", null)]);
        repository
            .AddAsync(Arg.Any<Supplier>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Supplier>());

        var fileContent = CreateWorkbook();
        var result = await context.Sender.Send(
            new ImportSuppliersCommand("suppliers.xlsx", fileContent));

        Assert.True(result.IsSuccess);
        var summary = Assert.IsType<SupplierImportSummary>(result.Data);
        Assert.Equal(4, summary.ProcessedRowCount);
        Assert.Equal(1, summary.ImportedCount);
        Assert.Equal(2, summary.DuplicateCount);
        Assert.Equal(1, summary.EmptyRowCount);
        Assert.Single(summary.InvalidRows);
        Assert.Equal(6, summary.InvalidRows[0].RowNumber);
        await repository.Received(1).AddAsync(
            Arg.Is<Supplier>(supplier => supplier.Title == "تأمین‌کننده جدید"),
            Arg.Any<CancellationToken>());
        await context.UnitOfWork.Received(1).SaveAsync(Arg.Any<CancellationToken>());
    }

    private static byte[] CreateWorkbook()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Suppliers");
        worksheet.Cell("B1").Value = "نام تأمین‌کننده";
        worksheet.Cell("B2").Value = "تأمین‌کننده موجود";
        worksheet.Cell("B3").Value = "تأمین‌کننده جدید";
        worksheet.Cell("B4").Value = "  تأمین‌کننده   جدید  ";
        worksheet.Cell("B6").Value = new string('ا', 201);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
