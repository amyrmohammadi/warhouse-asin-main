using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.People.Commands.CreatePerson;

public sealed record CreatePersonCommand(
    string FullName,
    string PhoneNumber) : IRequest<Result<PersonSummary>>;
