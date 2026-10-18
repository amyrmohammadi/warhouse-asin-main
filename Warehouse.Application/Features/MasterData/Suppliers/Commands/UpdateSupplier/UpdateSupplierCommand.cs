using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.Suppliers.Commands.UpdateSupplier;

public sealed record UpdateSupplierCommand(
    Guid Id,
    string Title,
    string? Description) : IRequest<Result<TitledMasterDataSummary>>;
