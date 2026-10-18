using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Title,
    string Code,
    string UnitOfMeasureTitle,
    string? Description) : IRequest<Result<ProductSummary>>;
