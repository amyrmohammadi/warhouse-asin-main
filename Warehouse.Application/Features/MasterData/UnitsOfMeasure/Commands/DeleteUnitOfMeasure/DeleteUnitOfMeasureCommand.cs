using MediatR;
using Warehouse.Application.Common.Models;

namespace Warehouse.Application.Features.MasterData.UnitsOfMeasure.Commands.DeleteUnitOfMeasure;

public sealed record DeleteUnitOfMeasureCommand(Guid Id) : IRequest<Result>;
