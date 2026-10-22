using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Application.Features.MasterData.Products.Queries.SearchProducts;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Tests.Features.MasterData.Products;

public sealed class SearchProductsFeatureTests
{
    [Fact]
    public async Task Search_ReturnsMatchingProductsWithUnitOrderedByTitle()
    {
        await using var context = new ApplicationTestContext();
        var unit = new UnitOfMeasure("عدد", null);
        Setup(context, unit, new[]
        {
            new Product("کالای ب", "B2", unit.Id, null),
            new Product("کالای الف", "A1", unit.Id, "توضیحات"),
            new Product("ماشین‌آلات", "X9", unit.Id, null)
        });

        var result = await context.Sender.Send(new SearchProductsQuery { Search = "کالای" });

        Assert.True(result.IsSuccess);
        var items = Assert.IsAssignableFrom<IReadOnlyCollection<ProductSummary>>(result.Data);
        Assert.Equal(2, items.Count);
        Assert.Equal("کالای الف", items.First().Title);
        Assert.Equal("A1", items.First().Code);
        Assert.Equal("عدد", items.First().UnitOfMeasureTitle);
        Assert.Equal(unit.Id, items.First().UnitOfMeasureId);
    }

    [Fact]
    public async Task Search_MatchesProductCode()
    {
        await using var context = new ApplicationTestContext();
        var unit = new UnitOfMeasure("عدد", null);
        Setup(context, unit, new[]
        {
            new Product("کالای الف", "A1", unit.Id, null),
            new Product("کالای ب", "B2", unit.Id, null)
        });

        var result = await context.Sender.Send(new SearchProductsQuery { Search = "B2" });

        Assert.True(result.IsSuccess);
        var items = Assert.IsAssignableFrom<IReadOnlyCollection<ProductSummary>>(result.Data);
        Assert.Single(items);
        Assert.Equal("کالای ب", items.Single().Title);
    }

    [Fact]
    public async Task Search_TrimsSearchTerm()
    {
        await using var context = new ApplicationTestContext();
        var unit = new UnitOfMeasure("عدد", null);
        Setup(context, unit, new[]
        {
            new Product("کالای الف", "A1", unit.Id, null),
            new Product("کالای ب", "B2", unit.Id, null)
        });

        var result = await context.Sender.Send(new SearchProductsQuery { Search = "  کالای الف  " });

        Assert.True(result.IsSuccess);
        var items = Assert.IsAssignableFrom<IReadOnlyCollection<ProductSummary>>(result.Data);
        Assert.Single(items);
        Assert.Equal("کالای الف", items.Single().Title);
    }

    [Fact]
    public async Task Search_ReturnsAtMostThirtyRows()
    {
        await using var context = new ApplicationTestContext();
        var unit = new UnitOfMeasure("عدد", null);
        Setup(context, unit, Enumerable.Range(1, 45)
            .Select(index => new Product($"کالای {index:D2}", $"C{index:D2}", unit.Id, null))
            .ToArray());

        var result = await context.Sender.Send(new SearchProductsQuery { Search = "کالای" });

        Assert.True(result.IsSuccess);
        var items = Assert.IsAssignableFrom<IReadOnlyCollection<ProductSummary>>(result.Data);
        Assert.Equal(SearchProductsQuery.MaximumResults, items.Count);
    }

    [Fact]
    public async Task Search_AppliesRequestedLimit()
    {
        await using var context = new ApplicationTestContext();
        var unit = new UnitOfMeasure("عدد", null);
        Setup(context, unit, Enumerable.Range(1, 10)
            .Select(index => new Product($"کالای {index:D2}", $"C{index:D2}", unit.Id, null))
            .ToArray());

        var result = await context.Sender.Send(new SearchProductsQuery
        {
            Search = "کالای",
            Limit = 3
        });

        Assert.True(result.IsSuccess);
        var items = Assert.IsAssignableFrom<IReadOnlyCollection<ProductSummary>>(result.Data);
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task Search_WithSingleCharacterSearchTerm_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(new SearchProductsQuery { Search = "ک" }));
    }

    [Fact]
    public async Task Search_WithEmptySearchTerm_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(new SearchProductsQuery { Search = "   " }));
    }

    [Fact]
    public async Task Search_WithLimitAboveMaximum_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(new SearchProductsQuery
            {
                Search = "کالا",
                Limit = SearchProductsQuery.MaximumResults + 1
            }));
    }

    private static void Setup(
        ApplicationTestContext context,
        UnitOfMeasure unit,
        IEnumerable<Product> products)
    {
        var productRepository = Substitute.For<IRepositoryAsync<Product, Guid>>();
        var unitRepository = Substitute.For<IRepositoryAsync<UnitOfMeasure, Guid>>();
        context.UnitOfWork.Repository<Product>().Returns(productRepository);
        context.UnitOfWork.Repository<UnitOfMeasure>().Returns(unitRepository);
        productRepository.Entities.Returns(products.AsQueryable());
        unitRepository.Entities.Returns(new List<UnitOfMeasure> { unit }.AsQueryable());
        productRepository
            .ToListAsync(
                Arg.Any<IQueryable<ProductSummary>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<ProductSummary>>().ToList());
    }
}
