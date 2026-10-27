using MediatR;
using Warehouse.Application.Common.Models;
using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Enums;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts.Commands.CreateGoodsReceipt;

public sealed record CreateGoodsReceiptCommand(
    DateTimeOffset EnteredAtUtc,
    string? Description,
    Guid? DelivererId,
    string? DelivererFullName,
    string PageNumber,
    IReadOnlyCollection<CreateGoodsReceiptItem> Items)
    : IRequest<Result<GoodsReceiptSummary>>;

public sealed record CreateGoodsReceiptItem(
    Guid? ProductId,
    string? ProductTitle,
    string? ProductCode,
    Guid? UnitOfMeasureId,
    Guid WarehouseId,
    InvoiceStatus? InvoiceStatus,
    string? InvoiceNumber,
    Guid? SupplierId,
    string? SupplierTitle,
    string? ReceiptNumber,
    decimal Quantity,
    string? RequestNumber,
    string? Description,
    string? IssueNumber = null);
