using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Application.Features.MasterData.Suppliers.Queries.SearchSuppliers;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Tests.Features.MasterData.Suppliers;

public sealed class SearchSuppliersFeatureTests
{
    [Fact]
    public async Task Search_ReturnsMatchingSuppliersOrderedByTitle()
    {
        await using var context = new ApplicationTestContext();
        var repository = Substitute.For<IRepositoryAsync<Supplier, Guid>>();
        context.UnitOfWork.Repository<Supplier>().Returns(repository);
        repository.Entities.Returns(new List<Supplier>
        {
            new("تأمین‌کننده ب", null),
            new("شرکت پارس", null),
            new("تأمین‌کننده الف", "توضیحات")
        }.AsQueryable());
        repository
            .ToListAsync(
                Arg.Any<IQueryable<TitledMasterDataSummary>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<TitledMasterDataSummary>>().ToList());

        var result = await context.Sender.Send(new SearchSuppliersQuery
        {
            Search = "تأمین"
        });

        Assert.True(result.IsSuccess);
        var items = Assert.IsAssignableFrom<IReadOnlyCollection<TitledMasterDataSummary>>(result.Data);
        Assert.Equal(2, items.Count);
        Assert.Equal("تأمین‌کننده الف", items.First().Title);
        Assert.Equal("تأمین‌کننده ب", items.Last().Title);
    }

    [Fact]
    public async Task Search_TrimsSearchTerm()
    {
        await using var context = new ApplicationTestContext();
        var repository = Substitute.For<IRepositoryAsync<Supplier, Guid>>();
        context.UnitOfWork.Repository<Supplier>().Returns(repository);
        repository.Entities.Returns(new List<Supplier>
        {
            new("تأمین‌کننده الف", null),
            new("شرکت پارس", null)
        }.AsQueryable());
        repository
            .ToListAsync(
                Arg.Any<IQueryable<TitledMasterDataSummary>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<TitledMasterDataSummary>>().ToList());

        var result = await context.Sender.Send(new SearchSuppliersQuery
        {
            Search = "  تأمین  "
        });

        Assert.True(result.IsSuccess);
        var items = Assert.IsAssignableFrom<IReadOnlyCollection<TitledMasterDataSummary>>(result.Data);
        Assert.Single(items);
        Assert.Equal("تأمین‌کننده الف", items.Single().Title);
    }

    [Fact]
    public async Task Search_ReturnsAtMostThirtyRows()
    {
        await using var context = new ApplicationTestContext();
        var repository = Substitute.For<IRepositoryAsync<Supplier, Guid>>();
        context.UnitOfWork.Repository<Supplier>().Returns(repository);
        repository.Entities.Returns(
            Enumerable.Range(1, 45)
                .Select(index => new Supplier($"تأمین‌کننده {index:D2}", null))
                .ToList()
                .AsQueryable());
        repository
            .ToListAsync(
                Arg.Any<IQueryable<TitledMasterDataSummary>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<TitledMasterDataSummary>>().ToList());

        var result = await context.Sender.Send(new SearchSuppliersQuery
        {
            Search = "تأمین"
        });

        Assert.True(result.IsSuccess);
        var items = Assert.IsAssignableFrom<IReadOnlyCollection<TitledMasterDataSummary>>(result.Data);
        Assert.Equal(SearchSuppliersQuery.MaximumResults, items.Count);
    }

    [Fact]
    public async Task Search_AppliesRequestedLimit()
    {
        await using var context = new ApplicationTestContext();
        var repository = Substitute.For<IRepositoryAsync<Supplier, Guid>>();
        context.UnitOfWork.Repository<Supplier>().Returns(repository);
        repository.Entities.Returns(
            Enumerable.Range(1, 10)
                .Select(index => new Supplier($"تأمین‌کننده {index:D2}", null))
                .ToList()
                .AsQueryable());
        repository
            .ToListAsync(
                Arg.Any<IQueryable<TitledMasterDataSummary>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IQueryable<TitledMasterDataSummary>>().ToList());

        var result = await context.Sender.Send(new SearchSuppliersQuery
        {
            Search = "تأمین",
            Limit = 3
        });

        Assert.True(result.IsSuccess);
        var items = Assert.IsAssignableFrom<IReadOnlyCollection<TitledMasterDataSummary>>(result.Data);
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task Search_WithSingleCharacterSearchTerm_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(new SearchSuppliersQuery { Search = "ا" }));
    }

    [Fact]
    public async Task Search_WithEmptySearchTerm_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(new SearchSuppliersQuery { Search = "   " }));
    }

    [Fact]
    public async Task Search_WithLimitAboveMaximum_ThrowsValidationException()
    {
        await using var context = new ApplicationTestContext();

        await Assert.ThrowsAsync<ValidationException>(() =>
            context.Sender.Send(new SearchSuppliersQuery
            {
                Search = "تأمین",
                Limit = SearchSuppliersQuery.MaximumResults + 1
            }));
    }
}
