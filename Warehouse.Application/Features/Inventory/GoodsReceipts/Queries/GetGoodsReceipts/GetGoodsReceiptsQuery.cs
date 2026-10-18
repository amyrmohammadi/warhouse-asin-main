using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Filtering;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Queries.GetGoodsReceipts;

public sealed record GetGoodsReceiptsQuery
    : DataTableParameters, IRequest<Result<GoodsReceiptListResult>>
{
    public DateTimeOffset? EnteredFromUtc { get; init; }

    public DateTimeOffset? EnteredToUtc { get; init; }

    public string? PageNumberFilter { get; init; }

    public InvoiceStatus? InvoiceStatus { get; init; }
}
