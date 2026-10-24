using MediatR;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.MasterData.People.Queries.GetPeople;

internal sealed class GetPeopleQueryHandler(IUnitOfWork<Guid> unitOfWork)
    : IRequestHandler<GetPeopleQuery, Result<MasterDataListResult<PersonSummary>>>
{
    public async Task<Result<MasterDataListResult<PersonSummary>>> Handle(
        GetPeopleQuery request,
        CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<Person>();
        var query = repository.Entities;
        var totalCount = await repository.CountAsync(query, cancellationToken);
        var search = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(person =>
                person.FullName.Contains(search) ||
                person.PhoneNumber.Contains(search));
        }

        var fullName = request.FullName?.Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            query = query.Where(person => person.FullName.Contains(fullName));
        }

        var phoneNumber = request.PhoneNumber?.Trim();
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            query = query.Where(person => person.PhoneNumber.Contains(phoneNumber));
        }

        var filteredCount = await repository.CountAsync(query, cancellationToken);
        query = ApplySorting(query, request.SortBy, request.SortDescending);

        if (request.PaginationEnabled)
        {
            query = query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize);
        }

        var page = await repository.ToListAsync(query, cancellationToken);
        var items = page
            .Select(person => new PersonSummary
            {
                Id = person.Id,
                ConcurrencyStamp = person.ConcurrencyStamp,
                FullName = person.FullName,
                PhoneNumber = person.PhoneNumber
            })
            .ToArray();
        var result = new MasterDataListResult<PersonSummary>(
            items,
            totalCount,
            filteredCount,
            request.PaginationEnabled ? request.PageNumber : 1,
            request.PaginationEnabled ? request.PageSize : items.Length,
            request.PaginationEnabled);

        return Result<MasterDataListResult<PersonSummary>>.Success(result);
    }

    private static IQueryable<Person> ApplySorting(
        IQueryable<Person> query,
        string? sortBy,
        bool descending) =>
        (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("id", false) => query.OrderBy(person => person.Id),
            ("id", true) => query.OrderByDescending(person => person.Id),
            ("phonenumber", false) => query.OrderBy(person => person.PhoneNumber)
                .ThenBy(person => person.Id),
            ("phonenumber", true) => query.OrderByDescending(person => person.PhoneNumber)
                .ThenByDescending(person => person.Id),
            ("fullname", true) => query.OrderByDescending(person => person.FullName)
                .ThenByDescending(person => person.Id),
            _ => query.OrderBy(person => person.FullName).ThenBy(person => person.Id)
        };
}
