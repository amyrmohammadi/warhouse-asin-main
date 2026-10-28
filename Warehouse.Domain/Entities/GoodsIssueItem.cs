using Warehouse.Domain.Common;
using Warehouse.Domain.Enums;

namespace Warehouse.Domain.Entities;

public sealed class GoodsIssueItem : Entity<Guid>
{
    private GoodsIssueItem() { }

    internal GoodsIssueItem(
        Guid goodsIssueId,
        Guid? productId,
        string productCode,
        decimal quantity,
        string? assetNumber,
        Guid? destinationId,
        Guid? shippingLocationId,
        GoodsIssueExitReason exitReasonType,
        bool isReturnable,
        string? legacyExitReason,
        string? description,
        string? deliveringUnit,
        DateTimeOffset? returnAtUtc,
        int sortOrder)
    {
        Id = Guid.NewGuid();
        GoodsIssueId = goodsIssueId;
        ProductId = productId;
        ProductCode = productCode.Trim();
        Quantity = quantity;
        AssetNumber = Normalize(assetNumber);
        DestinationId = destinationId;
        ShippingLocationId = shippingLocationId;
        ExitReasonType = exitReasonType;
        IsReturnable = isReturnable;
        ExitReason = Normalize(legacyExitReason);
        Description = Normalize(description);
        DeliveringUnit = Normalize(deliveringUnit);
        ReturnAtUtc = returnAtUtc?.ToUniversalTime();
        SortOrder = sortOrder;
    }

    public Guid GoodsIssueId { get; private set; }
    public GoodsIssue GoodsIssue { get; private set; } = null!;
    public Guid? ProductId { get; private set; }
    public Product? Product { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string? AssetNumber { get; private set; }
    public Guid? DestinationId { get; private set; }
    public GoodsIssueDestination? Destination { get; private set; }
    public Guid? ShippingLocationId { get; private set; }
    public Supplier? ShippingLocation { get; private set; }
    public GoodsIssueExitReason ExitReasonType { get; private set; }
    public bool IsReturnable { get; private set; } = true;
    // متن قدیمی برای حفظ اطلاعات تاریخی قبل از enum شدن علت خروج.
    public string? ExitReason { get; private set; }
    public string? Description { get; private set; }
    public string? DeliveringUnit { get; private set; }
    public DateTimeOffset? ReturnAtUtc { get; private set; }
    public int SortOrder { get; private set; }
    public ICollection<GoodsReceiptItem> GoodsReceiptItems { get; private set; } = [];

    public void SetReturnAtUtc(DateTimeOffset? returnAtUtc) =>
        ReturnAtUtc = returnAtUtc?.ToUniversalTime();

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
