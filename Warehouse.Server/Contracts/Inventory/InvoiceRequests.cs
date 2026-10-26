namespace Warehouse.Server.Contracts.Inventory;

public sealed record UpdateInvoiceItemRequest(
    string? ReceiptNumber,
    Guid ConcurrencyStamp);
