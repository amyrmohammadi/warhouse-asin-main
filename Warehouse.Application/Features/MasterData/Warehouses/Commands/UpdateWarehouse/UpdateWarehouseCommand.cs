using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.Warehouses.Commands.UpdateWarehouse;

public sealed record UpdateWarehouseCommand(
    Guid Id,
    string Title,
    string? Description) : IRequest<Result<TitledMasterDataSummary>>;
