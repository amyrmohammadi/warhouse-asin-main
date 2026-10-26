using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.Invoices;

internal static class InvoiceProjection
{
    public static IQueryable<InvoiceSummary> ToSummary(this IQueryable<Invoice> query) =>
        query.Select(invoice => new InvoiceSummary
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            Counterparty = invoice.Supplier == null ? null : invoice.Supplier.Title,
            ReceiptNumbers = invoice.GoodsReceiptItems
                .Where(item => item.ReceiptNumber != null)
                .Select(item => item.ReceiptNumber!)
                .ToArray(),
            ItemsWithoutReceiptCount = invoice.GoodsReceiptItems.Count(
                item => item.ReceiptNumber == null)
        });

    public static IQueryable<InvoiceDetails> ToDetails(this IQueryable<Invoice> query) =>
        query.Select(invoice => new InvoiceDetails
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            Counterparty = invoice.Supplier == null ? null : invoice.Supplier.Title,
            Items = invoice.GoodsReceiptItems
                .OrderBy(item => item.GoodsReceipt.EnteredAtUtc)
                .ThenBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select(item => new InvoiceItemSummary
                {
                    Id = item.Id,
                    GoodsReceiptId = item.GoodsReceiptId,
                    GoodsReceiptConcurrencyStamp = item.GoodsReceipt.ConcurrencyStamp,
                    ProductTitle = item.Product.Title,
                    ProductCode = item.Product.Code,
                    UnitOfMeasureTitle = item.Product.UnitOfMeasure.Title,
                    Quantity = item.Quantity,
                    WarehouseTitle = item.Warehouse.Title,
                    EnteredAtUtc = item.GoodsReceipt.EnteredAtUtc,
                    PageNumber = item.GoodsReceipt.PageNumber,
                    ReceiptNumber = item.ReceiptNumber,
                    Description = item.Description
                })
                .ToArray()
        });
}
