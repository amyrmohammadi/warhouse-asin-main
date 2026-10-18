using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptOptions;

public sealed record GetGoodsReceiptOptionsQuery : IRequest<Result<GoodsReceiptOptions>>;
