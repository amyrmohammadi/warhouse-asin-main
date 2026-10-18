using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Features.MasterData.Products.Queries.GetProductById;

public sealed record GetProductByIdQuery(Guid Id) : IRequest<Result<ProductSummary>>;
