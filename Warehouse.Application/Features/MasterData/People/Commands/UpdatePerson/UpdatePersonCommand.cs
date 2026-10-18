using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.People.Commands.UpdatePerson;

public sealed record UpdatePersonCommand(
    Guid Id,
    string FullName,
    string PhoneNumber) : IRequest<Result<PersonSummary>>;
