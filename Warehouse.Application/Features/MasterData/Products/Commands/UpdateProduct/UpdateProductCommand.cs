using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.Products.Commands.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid Id,
    string Title,
    string Code,
    string UnitOfMeasureTitle,
    string? Description) : IRequest<Result<ProductSummary>>;
