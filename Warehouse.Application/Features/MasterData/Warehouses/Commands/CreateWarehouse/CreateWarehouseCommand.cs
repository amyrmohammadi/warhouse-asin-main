using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.Warehouses.Commands.CreateWarehouse;

public sealed record CreateWarehouseCommand(
    string Title,
    string? Description) : IRequest<Result<TitledMasterDataSummary>>;
