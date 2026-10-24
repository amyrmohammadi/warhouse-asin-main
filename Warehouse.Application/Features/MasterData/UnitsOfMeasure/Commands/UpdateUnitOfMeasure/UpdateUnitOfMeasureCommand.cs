using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.UnitsOfMeasure.Commands.UpdateUnitOfMeasure;

public sealed record UpdateUnitOfMeasureCommand(
    Guid Id,
    string Title,
    string? Description,
    Guid ConcurrencyStamp = default) : IRequest<Result<TitledMasterDataSummary>>;
