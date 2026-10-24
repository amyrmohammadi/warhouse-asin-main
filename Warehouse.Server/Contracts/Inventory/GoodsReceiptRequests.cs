using Warehouse.Domain.Enums;

namespace Warehouse.Server.Contracts.Inventory;

public sealed record CreateGoodsReceiptRequest(
    DateTimeOffset EnteredAtUtc,
    string? Description,
    Guid? DelivererId,
    string? DelivererFullName,
    string PageNumber,
    IReadOnlyCollection<CreateGoodsReceiptItemRequest> Items,
    Guid? ConcurrencyStamp = null);

public sealed record CreateGoodsReceiptItemRequest(
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
    string? Description);


public sealed class ImportGoodsReceiptsRequest
{
    public IFormFile? File { get; init; }
}
