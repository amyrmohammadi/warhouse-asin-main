using Warehouse.Domain.Common;

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
        Guid destinationId,
        string exitReason,
        string? description,
        string deliveringUnit,
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
        ExitReason = exitReason.Trim();
        Description = Normalize(description);
        DeliveringUnit = deliveringUnit.Trim();
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
    public Guid DestinationId { get; private set; }
    public GoodsIssueDestination Destination { get; private set; } = null!;
    public string ExitReason { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string DeliveringUnit { get; private set; } = string.Empty;
    public DateTimeOffset? ReturnAtUtc { get; private set; }
    public int SortOrder { get; private set; }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
