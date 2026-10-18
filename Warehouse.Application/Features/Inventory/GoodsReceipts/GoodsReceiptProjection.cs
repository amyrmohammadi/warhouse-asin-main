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
            WarehouseId = receipt.WarehouseId,
            WarehouseTitle = receipt.Warehouse.Title,
            InvoiceStatus = receipt.InvoiceStatus,
            InvoiceNumber = receipt.InvoiceNumber,
            SupplierId = receipt.SupplierId,
            Counterparty = receipt.Supplier == null ? null : receipt.Supplier.Title,
            ReceiptNumber = receipt.ReceiptNumber,
            PageNumber = receipt.PageNumber,
            Items = receipt.Items
                .OrderBy(item => item.Product.Title)
                .Select(item => new GoodsReceiptItemSummary
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductTitle = item.Product.Title,
                    ProductCode = item.Product.Code,
                    UnitOfMeasureTitle = item.Product.UnitOfMeasure.Title,
                    Quantity = item.Quantity,
                    RequestNumber = item.RequestNumber,
                    Description = item.Description
                })
                .ToArray()
        });
}
