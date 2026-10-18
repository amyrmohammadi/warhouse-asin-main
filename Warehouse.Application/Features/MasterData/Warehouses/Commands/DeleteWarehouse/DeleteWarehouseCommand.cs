using MediatR;
using Warehouse.Application.Common.Models;

namespace Warehouse.Application.Features.MasterData.Warehouses.Commands.DeleteWarehouse;

public sealed record DeleteWarehouseCommand(Guid Id) : IRequest<Result>;
