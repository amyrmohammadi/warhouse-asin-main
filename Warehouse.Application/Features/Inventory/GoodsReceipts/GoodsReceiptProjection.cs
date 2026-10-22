using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsReceipts;

internal static class GoodsReceiptProjection
{
    public static IQueryable<GoodsReceiptSummary> ToSummary(
        this IQueryable<GoodsReceipt> query) =>
        query.Select(receipt => new GoodsReceiptSummary
        {
            Id = receipt.Id,
            EnteredAtUtc = receipt.EnteredAtUtc,
            Description = receipt.Description,
            DelivererId = receipt.DelivererId,
            DelivererFullName = receipt.Deliverer.FullName,
            PageNumber = receipt.PageNumber,
            Items = receipt.Items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select(item => new GoodsReceiptItemSummary
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductTitle = item.Product.Title,
                    ProductCode = item.Product.Code,
                    UnitOfMeasureId = item.Product.UnitOfMeasureId,
                    UnitOfMeasureTitle = item.Product.UnitOfMeasure.Title,
                    WarehouseId = item.WarehouseId,
                    WarehouseTitle = item.Warehouse.Title,
                    InvoiceStatus = item.InvoiceStatus,
                    InvoiceNumber = item.InvoiceNumber,
                    SupplierId = item.SupplierId,
                    Counterparty = item.Supplier == null ? null : item.Supplier.Title,
                    ReceiptNumber = item.ReceiptNumber,
                    Quantity = item.Quantity,
                    RequestNumber = item.RequestNumber,
                    Description = item.Description
                })
                .ToArray()
        });
}
