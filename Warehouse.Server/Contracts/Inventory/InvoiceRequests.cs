using Warehouse.Domain.Enums;

namespace Warehouse.Server.Contracts.Inventory;

public sealed record UpdateInvoiceRequest(
    string InvoiceNumber,
    Guid? SupplierId,
    string? Description,
    DateOnly? InvoiceDate,
    InvoiceProcessingStatus ProcessingStatus,
    IReadOnlyCollection<UpdateInvoiceReceiptItemRequest> Items);

public sealed record UpdateInvoiceReceiptItemRequest(
    Guid Id,
    string? ReceiptNumber,
    Guid GoodsReceiptConcurrencyStamp);

public sealed record UpdateInvoiceItemRequest(
    string? ReceiptNumber,
    Guid ConcurrencyStamp);
