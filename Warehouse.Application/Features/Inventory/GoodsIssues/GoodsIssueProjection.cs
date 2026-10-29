using Warehouse.Application.Common.Models.Inventory;
using Warehouse.Domain.Entities;

namespace Warehouse.Application.Features.Inventory.GoodsIssues;

internal static class GoodsIssueProjection
{
    public static IQueryable<GoodsIssueSummary> ToSummary(this IQueryable<GoodsIssue> query) =>
        query.Select(issue => new GoodsIssueSummary
        {
            Id = issue.Id,
            IsFinalized = issue.IsFinalized,
            ConcurrencyStamp = issue.ConcurrencyStamp,
            IssueNumber = issue.IssueNumber,
            IssuedAtUtc = issue.IssuedAtUtc,
            Description = issue.Description,
            IssuerId = issue.IssuerId,
            IssuerFullName = issue.Issuer.FullName,
            Items = issue.Items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select(item => new GoodsIssueItemSummary
                {
                    Id = item.Id,
                    GoodsReceiptItemIds = item.GoodsReceiptItems.Select(receiptItem => receiptItem.Id).ToArray(),
                    ProductId = item.ProductId,
                    ProductTitle = item.Product == null ? null : item.Product.Title,
                    ProductUnitOfMeasureTitle = item.Product == null ? null : item.Product.UnitOfMeasure.Title,
                    ProductCode = item.ProductCode,
                    Quantity = item.Quantity,
                    AssetNumber = item.AssetNumber,
                    DestinationId = item.DestinationId,
                    DestinationTitle = item.Destination == null ? null : item.Destination.Title,
                    ShippingLocationId = item.ShippingLocationId,
                    ShippingLocationTitle = item.ShippingLocation == null ? null : item.ShippingLocation.Title,
                    ExitReason = item.ExitReasonType,
                    LegacyExitReason = item.ExitReason,
                    IsReturnable = item.IsReturnable,
                    Description = item.Description,
                    DeliveringUnit = item.DeliveringUnit,
                    ReturnAtUtc = item.ReturnAtUtc
                })
                .ToArray()
        });
}
