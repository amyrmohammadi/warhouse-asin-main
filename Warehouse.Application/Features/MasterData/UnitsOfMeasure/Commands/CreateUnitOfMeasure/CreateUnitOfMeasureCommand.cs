using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.UnitsOfMeasure.Commands.CreateUnitOfMeasure;

public sealed record CreateUnitOfMeasureCommand(
    string Title,
    string? Description) : IRequest<Result<TitledMasterDataSummary>>;
