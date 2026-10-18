using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.Suppliers.Queries.GetSupplierById;

public sealed record GetSupplierByIdQuery(Guid Id)
    : IRequest<Result<TitledMasterDataSummary>>;
