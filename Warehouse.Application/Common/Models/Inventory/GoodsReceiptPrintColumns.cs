namespace Warehouse.Application.Common.Models.Inventory;

public sealed record GoodsReceiptPrintColumn(string Key, string Title);

public static class GoodsReceiptPrintColumns
{
    public static IReadOnlyList<GoodsReceiptPrintColumn> Items { get; } =
    [
        new("id", "ردیف"), new("pageNumber", "شماره صفحه"), new("enteredAtUtc", "تاریخ"),
        new("productTitle", "عنوان کالا"), new("productCode", "کد کالا"), new("quantity", "تعداد"),
        new("unitOfMeasureTitle", "واحد"), new("itemDescription", "توضیحات کالا"),
        new("delivererFullName", "تحویل‌دهنده"), new("invoiceStatus", "فاکتور"),
        new("invoiceNumber", "شماره فاکتور"), new("counterparty", "طرف حساب"),
        new("receiptNumber", "شماره رسید"), new("issueNumber", "شماره خروج"), new("warehouseTitle", "انبار")
    ];

    public static IReadOnlyList<GoodsReceiptPrintColumn> Receipts { get; } =
    [
        new("id", "ردیف"), new("enteredAtUtc", "تاریخ ورود"), new("pageNumber", "شماره صفحه"),
        new("delivererFullName", "تحویل‌دهنده"), new("itemCount", "تعداد کالا"),
        new("itemTitles", "اقلام"), new("description", "توضیحات ورودی")
    ];
}
