using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.People.Queries.GetPersonById;

public sealed record GetPersonByIdQuery(Guid Id) : IRequest<Result<PersonSummary>>;
