using MediatR;
using Warehouse.Application.Common.Models;

namespace Warehouse.Application.Features.MasterData.People.Commands.DeletePerson;

public sealed record DeletePersonCommand(Guid Id) : IRequest<Result>;
