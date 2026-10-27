using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.Inventory;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceipts;

public sealed record GetGoodsReceiptsQuery
    : DataTableParameters, IRequest<Result<GoodsReceiptListResult>>
{
    public DateFilter? EnteredAt { get; init; }

    public TextFilter? PageNumberFilter { get; init; }

    public InvoiceStatusFilter? InvoiceStatus { get; init; }
    public GoodsReceiptQuickFilter? QuickFilter { get; init; }

    public TextFilter? ProductTitle { get; init; }

    public TextFilter? ProductCode { get; init; }

    public NumericFilter<decimal>? Quantity { get; init; }

    public TextFilter? InvoiceNumber { get; init; }

    public TextFilter? ReceiptNumber { get; init; }
    public bool ReceiptNumberIsEmpty { get; init; }

    public Guid? WarehouseId { get; init; }

    public Guid? DelivererId { get; init; }

    public Guid? SupplierId { get; init; }
}
