using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.People.Queries.GetPeople;

public sealed record GetPeopleQuery
    : DataTableParameters, IRequest<Result<MasterDataListResult<PersonSummary>>>
{
    public string? FullName { get; init; }

    public string? PhoneNumber { get; init; }
}
