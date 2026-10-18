using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptById;

public sealed record GetGoodsReceiptByIdQuery(Guid Id)
    : IRequest<Result<GoodsReceiptSummary>>;
