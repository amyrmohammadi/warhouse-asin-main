using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.UnitsOfMeasure.Queries.GetUnitOfMeasureById;

public sealed record GetUnitOfMeasureByIdQuery(Guid Id)
    : IRequest<Result<TitledMasterDataSummary>>;
