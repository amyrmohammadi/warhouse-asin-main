using ClosedXML.Excel;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.Inventory.GoodsIssues.Commands.ImportGoodsIssues;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Tests.Features.Inventory.GoodsIssues;

public sealed class ImportGoodsIssuesTests
{
    [Fact]
    public async Task Import_GroupsRowsInSheetOrder_AndCreatesOnlyMissingIssuer()
    {
        await using var context = new ApplicationTestContext();
        var unit = new UnitOfMeasure("عدد", null);
        var product = new Product("کالا", "P-1", unit.Id, null);
        var destination = new GoodsIssueDestination("تهران", null);
        var products = Substitute.For<IRepositoryAsync<Product, Guid>>();
        var units = Substitute.For<IRepositoryAsync<UnitOfMeasure, Guid>>();
        var people = Substitute.For<IRepositoryAsync<Person, Guid>>();
        var destinations = Substitute.For<IRepositoryAsync<GoodsIssueDestination, Guid>>();
        var issues = Substitute.For<IRepositoryAsync<GoodsIssue, Guid>>();
        context.UnitOfWork.Repository<Product>().Returns(products);
        context.UnitOfWork.Repository<UnitOfMeasure>().Returns(units);
        context.UnitOfWork.Repository<Person>().Returns(people);
        context.UnitOfWork.Repository<GoodsIssueDestination>().Returns(destinations);
        context.UnitOfWork.Repository<GoodsIssue>().Returns(issues);
        products.GetAllAsync(Arg.Any<CancellationToken>()).Returns([product]);
        units.GetAllAsync(Arg.Any<CancellationToken>()).Returns([unit]);
        people.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Person>());
        destinations.GetAllAsync(Arg.Any<CancellationToken>()).Returns([destination]);
        issues.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<GoodsIssue>());

        var file = FileWithRows(("کالا", "محمد", 2), ("کالا", "", 1));
        var result = await context.Sender.Send(new ImportGoodsIssuesCommand("issues.xlsx", file));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.InvalidRows);
        Assert.Equal(1, result.Data.ImportedIssueCount);
        Assert.Equal(2, result.Data.ImportedItemCount);
        Assert.Equal(1, result.Data.CreatedIssuerCount);
        await issues.Received(1).AddAsync(
            Arg.Is<GoodsIssue>(issue => issue.IssueNumber == "99/03351" &&
                issue.Items.OrderBy(item => item.SortOrder).Select(item => item.Quantity).SequenceEqual(new decimal[] { 2, 1 }) &&
                issue.Items.OrderBy(item => item.SortOrder).First().DestinationId == null &&
                issue.Items.OrderBy(item => item.SortOrder).First().DeliveringUnit == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Import_RejectsMissingProductWithoutSavingAnything()
    {
        await using var context = new ApplicationTestContext();
        var products = Substitute.For<IRepositoryAsync<Product, Guid>>();
        var units = Substitute.For<IRepositoryAsync<UnitOfMeasure, Guid>>();
        var people = Substitute.For<IRepositoryAsync<Person, Guid>>();
        var destinations = Substitute.For<IRepositoryAsync<GoodsIssueDestination, Guid>>();
        var issues = Substitute.For<IRepositoryAsync<GoodsIssue, Guid>>();
        context.UnitOfWork.Repository<Product>().Returns(products);
        context.UnitOfWork.Repository<UnitOfMeasure>().Returns(units);
        context.UnitOfWork.Repository<Person>().Returns(people);
        context.UnitOfWork.Repository<GoodsIssueDestination>().Returns(destinations);
        context.UnitOfWork.Repository<GoodsIssue>().Returns(issues);
        products.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Product>());
        units.GetAllAsync(Arg.Any<CancellationToken>()).Returns([new UnitOfMeasure("عدد", null)]);
        people.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Person>());
        destinations.GetAllAsync(Arg.Any<CancellationToken>()).Returns([new GoodsIssueDestination("تهران", null)]);
        issues.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<GoodsIssue>());

        var result = await context.Sender.Send(new ImportGoodsIssuesCommand("issues.xlsx", FileWithRows(("ناشناخته", "محمد", 1))));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.InvalidRows);
        Assert.Equal("1405", result.Data.InvalidRows[0].Sheet);
        Assert.Equal(2, result.Data.InvalidRows[0].RowNumber);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
        await people.DidNotReceive().AddAsync(Arg.Any<Person>(), Arg.Any<CancellationToken>());
    }

    private static byte[] FileWithRows(params (string Title, string Issuer, decimal Quantity)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("1405");
        string[] headers = ["ردیف", "تاریخ", "عنوان کالا", "تعداد", "واحد کالا", "شماره خروج",
            "خارج کننده", "مقصد", "علت خروج", "واحد تحویل دهنده", "تاریخ عودت", "توضیحات"];
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        for (var index = 0; index < rows.Length; index++)
        {
            var row = index + 2;
            sheet.Cell(row, 2).Value = "1405/01/09";
            sheet.Cell(row, 3).Value = rows[index].Title;
            sheet.Cell(row, 4).Value = rows[index].Quantity;
            sheet.Cell(row, 5).Value = "عدد";
            if (index == 0) sheet.Cell(row, 6).Value = 99.03351;
            else sheet.Cell(row, 6).Value = "99/03351";
            sheet.Cell(row, 7).Value = rows[index].Issuer;
            sheet.Cell(row, 8).Value = index == 0 ? "" : "تهران";
            sheet.Cell(row, 9).Value = "تعمیر";
            sheet.Cell(row, 10).Value = index == 0 ? "" : "انبار";
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
