using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.Inventory;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceiptItemRows;

public sealed record GetGoodsReceiptItemRowsQuery
    : DataTableParameters, IRequest<Result<GoodsReceiptItemRowListResult>>;
