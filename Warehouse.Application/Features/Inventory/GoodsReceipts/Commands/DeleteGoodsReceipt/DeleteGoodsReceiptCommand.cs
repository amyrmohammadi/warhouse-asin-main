using MediatR;
using Warehouse.Application.Common.Models;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.DeleteGoodsReceipt;

public sealed record DeleteGoodsReceiptCommand(Guid Id) : IRequest<Result>;
