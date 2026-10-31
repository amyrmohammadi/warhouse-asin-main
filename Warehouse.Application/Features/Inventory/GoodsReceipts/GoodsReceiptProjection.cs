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
            IsLocked = receipt.Items.Any(item => item.Invoice != null &&
                item.Invoice.ProcessingStatus != Warehouse.Domain.Enums.InvoiceProcessingStatus.UnderReview),
            ConcurrencyStamp = receipt.ConcurrencyStamp,
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
                    GoodsIssueItemId = item.GoodsIssueItemId,
                    IssueNumber = item.GoodsIssueItem == null ? null : item.GoodsIssueItem.GoodsIssue.IssueNumber,
                    ProductId = item.ProductId,
                    ProductTitle = item.Product.Title,
                    ProductCode = item.Product.Code,
                    UnitOfMeasureId = item.Product.UnitOfMeasureId,
                    UnitOfMeasureTitle = item.Product.UnitOfMeasure.Title,
                    WarehouseId = item.WarehouseId,
                    WarehouseTitle = item.Warehouse.Title,
                    InvoiceStatus = item.InvoiceStatus,
                    InvoiceNumber = item.Invoice == null ? null : item.Invoice.InvoiceNumber,
                    SupplierId = item.Invoice == null ? item.SupplierId : item.Invoice.SupplierId,
                    Counterparty = item.Invoice == null
                        ? item.Supplier == null ? null : item.Supplier.Title
                        : item.Invoice.Supplier == null ? null : item.Invoice.Supplier.Title,
                    ReceiptNumber = item.ReceiptNumber,
                    Quantity = item.Quantity,
                    RequestNumber = item.RequestNumber,
                    Description = item.Description
                })
                .ToArray()
        });
}
