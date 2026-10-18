using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.Warehouses.Queries.GetWarehouseById;

public sealed record GetWarehouseByIdQuery(Guid Id)
    : IRequest<Result<TitledMasterDataSummary>>;
