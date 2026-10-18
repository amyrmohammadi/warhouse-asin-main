using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.Suppliers.Commands.CreateSupplier;

public sealed record CreateSupplierCommand(
    string Title,
    string? Description) : IRequest<Result<TitledMasterDataSummary>>;
