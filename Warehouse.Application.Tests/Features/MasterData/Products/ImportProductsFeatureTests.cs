using ClosedXML.Excel;
using FluentValidation;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Features.MasterData.Products.Commands.ImportProducts;
using Warehouse.Application.Tests.TestSupport;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Tests.Features.MasterData.Products;

public sealed class ImportProductsFeatureTests
{
    [Fact]
    public async Task Import_ValidatesAllRows_AndDoesNotAddAnythingWhenAnyRowIsInvalid()
    {
        await using var context = new ApplicationTestContext();
        var products = Substitute.For<IRepositoryAsync<Product, Guid>>();
        var units = Substitute.For<IRepositoryAsync<UnitOfMeasure, Guid>>();
        context.UnitOfWork.Repository<Product>().Returns(products);
        context.UnitOfWork.Repository<UnitOfMeasure>().Returns(units);
        products.GetAllAsync(Arg.Any<CancellationToken>()).Returns([new Product("موجود", "A1", Guid.NewGuid(), null)]);
        var result = await context.Sender.Send(new ImportProductsCommand("products.xlsx", Workbook(
            ("نام کالا", "کد کالا", "واحد"),
            ("جدید", "001", "واحد جدید"),
            ("بدون کد", "", "عدد"),
            ("تکرار کد", "A1", "عدد"),
            ("ج-دید", "002", "عدد"),
            ("بدون واحد", "003", ""),
            ("فاصله", "  ", "عدد"))));
        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Data!.ProcessedRowCount);
        Assert.Equal(0, result.Data.ImportedCount);
        Assert.Equal(2, result.Data.DuplicateCount);
        Assert.Equal(new[] { 3, 4, 5, 6, 7 }, result.Data.InvalidRows.Select(r => r.RowNumber));
        Assert.Contains("ردیف 2 همین فایل", result.Data.InvalidRows.Single(r => r.RowNumber == 5).Message);
        Assert.Contains("«جدید»", result.Data.InvalidRows.Single(r => r.RowNumber == 5).Message);
        await products.DidNotReceive().AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
        await units.DidNotReceive().AddAsync(Arg.Any<UnitOfMeasure>(), Arg.Any<CancellationToken>());
        await context.UnitOfWork.DidNotReceive().BeginTransaction(Arg.Any<CancellationToken>());
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Import_StopsAtEmptyTitle_AndAcceptsRepeatedPlaceholderCodes()
    {
        await using var context = new ApplicationTestContext();
        var products = Substitute.For<IRepositoryAsync<Product, Guid>>();
        var units = Substitute.For<IRepositoryAsync<UnitOfMeasure, Guid>>();
        context.UnitOfWork.Repository<Product>().Returns(products);
        context.UnitOfWork.Repository<UnitOfMeasure>().Returns(units);
        var unit = new UnitOfMeasure("عدد", null);
        products.GetAllAsync(Arg.Any<CancellationToken>()).Returns([new Product("موجود", "-", unit.Id, null)]);
        units.GetAllAsync(Arg.Any<CancellationToken>()).Returns([unit]);
        var result = await context.Sender.Send(new ImportProductsCommand("products.xlsx", Workbook(
            ("اول", "-", "عدد"), ("دوم", " - ", "عدد"), ("سوم", "001", "کیلو"),
            ("  ", "nonempty", "nonempty"), ("نباید خوانده شود", "", ""))));
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.ProcessedRowCount);
        Assert.Equal(3, result.Data.ImportedCount);
        Assert.Equal(1, result.Data.EmptyRowCount);
        Assert.Empty(result.Data.InvalidRows);
        await products.Received(2).AddAsync(Arg.Is<Product>(p => p.Code == "-" && p.UnitOfMeasureId == unit.Id), Arg.Any<CancellationToken>());
        await products.Received(3).AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
        await units.Received(1).AddAsync(Arg.Is<UnitOfMeasure>(u => u.Title == "کیلو"), Arg.Any<CancellationToken>());
        await context.UnitOfWork.Received(1).CommitTransaction(Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task Import_InvalidWorkbook_ReturnsErrorWithoutSaving()
    {
        await using var context = new ApplicationTestContext();
        var result = await context.Sender.Send(new ImportProductsCommand("products.xlsx", [1, 2, 3]));
        Assert.False(result.IsSuccess);
        await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Import_NullContent_ReturnsValidationException()
    {
        await using var context = new ApplicationTestContext();
        await Assert.ThrowsAsync<ValidationException>(() => context.Sender.Send(new ImportProductsCommand("products.xlsx", null!)));
    }

    [Fact]
    public async Task Import_SaveFailure_RollsBack()
    {
        await using var context = new ApplicationTestContext();
        var products = Substitute.For<IRepositoryAsync<Product, Guid>>();
        var units = Substitute.For<IRepositoryAsync<UnitOfMeasure, Guid>>();
        context.UnitOfWork.Repository<Product>().Returns(products);
        context.UnitOfWork.Repository<UnitOfMeasure>().Returns(units);
        products.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Product>());
        units.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<UnitOfMeasure>());
        context.UnitOfWork.SaveAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<int>(new InvalidOperationException("save failed")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Sender.Send(
            new ImportProductsCommand("products.xlsx", Workbook(("کالا", "001", "عدد")))));
        await context.UnitOfWork.Received(1).RollBackTransaction(Arg.Any<CancellationToken>());
        await context.UnitOfWork.DidNotReceive().CommitTransaction(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, "NEW", 0)]
    [InlineData(true, "NEW", 2)]
    [InlineData(true, "-", 2)]
    [InlineData(true, "EXISTING", 0)]
    [InlineData(true, "", 0)]
    public async Task Import_CodeOnlyOption_ControlsTitleCheckButKeepsCodeValidation(
        bool codeOnly, string code, int expectedCount)
    {
        await using var context = new ApplicationTestContext();
        var products = Substitute.For<IRepositoryAsync<Product, Guid>>();
        var units = Substitute.For<IRepositoryAsync<UnitOfMeasure, Guid>>();
        var unit = new UnitOfMeasure("عدد", null);
        context.UnitOfWork.Repository<Product>().Returns(products);
        context.UnitOfWork.Repository<UnitOfMeasure>().Returns(units);
        products.GetAllAsync(Arg.Any<CancellationToken>()).Returns([
            new Product("عنوان یکسان", "EXISTING", unit.Id, null),
            new Product("قبلی", "-", unit.Id, null)]);
        units.GetAllAsync(Arg.Any<CancellationToken>()).Returns([unit]);
        var result = await context.Sender.Send(new ImportProductsCommand("products.xlsx", Workbook(
            ("عنوان یکسان", code, "عدد"),
            ("عنوان یکسان", code == "-" ? "-" : "SECOND", "عدد")), codeOnly));
        Assert.Equal(expectedCount, result.Data!.ImportedCount);
        if (expectedCount > 0)
        {
            Assert.Empty(result.Data.InvalidRows);
            await products.Received(expectedCount).AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
        }
        else
        {
            Assert.NotEmpty(result.Data.InvalidRows);
            await products.DidNotReceive().AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
            await context.UnitOfWork.DidNotReceive().SaveAsync(Arg.Any<CancellationToken>());
        }
    }
    private static byte[] Workbook(params (string Title, string Code, string Unit)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Products");
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 1, 2).Value = rows[i].Title;
            sheet.Cell(i + 1, 3).Value = rows[i].Code;
            sheet.Cell(i + 1, 4).Value = rows[i].Unit;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
