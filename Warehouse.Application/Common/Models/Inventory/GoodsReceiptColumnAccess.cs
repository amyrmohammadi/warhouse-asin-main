using Warehouse.Application.Common.Models.Auditing;

namespace Warehouse.Application.Common.Models.Inventory;

public sealed record GoodsReceiptColumnAccess(
    bool Warehouse, bool InvoiceStatus, bool Counterparty, bool InvoiceNumber, bool ReceiptNumber)
{
    public static GoodsReceiptColumnAccess All { get; } = new(true, true, true, true, true);

    public bool CanRead(string? field) => field?.Trim().ToLowerInvariant() switch
    {
        "warehouseid" or "warehousetitle" => Warehouse,
        "invoicestatus" => InvoiceStatus,
        "supplierid" or "counterparty" => Counterparty,
        "invoicenumber" => InvoiceNumber,
        "invoiceid" => InvoiceNumber && Counterparty,
        "receiptnumber" => ReceiptNumber,
        _ => true
    };

    public GoodsReceiptItemRowSummary Apply(GoodsReceiptItemRowSummary item) => item with
    {
        WarehouseTitle = Warehouse ? item.WarehouseTitle : string.Empty,
        InvoiceStatus = InvoiceStatus ? item.InvoiceStatus : null,
        SupplierId = Counterparty ? item.SupplierId : null,
        Counterparty = Counterparty ? item.Counterparty : null,
        InvoiceNumber = InvoiceNumber ? item.InvoiceNumber : null,
        ReceiptNumber = ReceiptNumber ? item.ReceiptNumber : null
    };

    public GoodsReceiptSummary Apply(GoodsReceiptSummary receipt) => receipt with
    {
        Items = receipt.Items.Select(item => item with
        {
            WarehouseId = Warehouse ? item.WarehouseId : Guid.Empty,
            WarehouseTitle = Warehouse ? item.WarehouseTitle : string.Empty,
            InvoiceStatus = InvoiceStatus ? item.InvoiceStatus : null,
            SupplierId = Counterparty ? item.SupplierId : null,
            Counterparty = Counterparty ? item.Counterparty : null,
            InvoiceNumber = InvoiceNumber ? item.InvoiceNumber : null,
            ReceiptNumber = ReceiptNumber ? item.ReceiptNumber : null
        }).ToArray()
    };

    public GoodsReceiptItemHistoryResult Apply(GoodsReceiptItemHistoryResult history) => history with
    {
        Entries = history.Entries.Select(entry => entry with
        {
            Changes = entry.Changes.Where(change => CanRead(change.FieldName)).ToArray()
        }).Where(entry => entry.Changes.Count > 0).ToArray()
    };
}
