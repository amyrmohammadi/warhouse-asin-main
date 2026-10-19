using Warehouse.Domain.Enums;

namespace Warehouse.Server.Contracts.Inventory;

public sealed record CreateGoodsReceiptRequest(
    DateTimeOffset EnteredAtUtc,
    string? Description,
    Guid? DelivererId,
    string? DelivererFullName,
    Guid WarehouseId,
    InvoiceStatus? InvoiceStatus,
    string? InvoiceNumber,
    Guid? SupplierId,
    string? SupplierTitle,
    string? ReceiptNumber,
    string PageNumber,
    IReadOnlyCollection<CreateGoodsReceiptItemRequest> Items);

public sealed record CreateGoodsReceiptItemRequest(
    Guid? ProductId,
    string? ProductTitle,
    string? ProductCode,
    Guid? UnitOfMeasureId,
    decimal Quantity,
    string? RequestNumber,
    string? Description);


public sealed class ImportGoodsReceiptsRequest
{
    public IFormFile? File { get; init; }
}
