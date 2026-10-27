namespace Warehouse.Application.Common.Models.Inventory;

public sealed record GoodsReceiptMetrics(
    int? ItemsWithoutReceiptCount,
    int? ItemsWithoutInvoiceCount,
    int? TodayItemCount,
    int? TodayDocumentCount);
