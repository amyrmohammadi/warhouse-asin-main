using Warehouse.Application.Common.Models.MasterData;

namespace Warehouse.Application.Common.Models.Inventory;

public sealed class GoodsIssueSummary
{
    public Guid Id { get; init; }
    public string IssueNumber { get; init; } = string.Empty;
    public DateTimeOffset IssuedAtUtc { get; init; }
    public Guid IssuerId { get; init; }
    public string IssuerFullName { get; init; } = string.Empty;
    public IReadOnlyCollection<GoodsIssueItemSummary> Items { get; init; } = [];
}

public class GoodsIssueItemSummary
{
    public Guid Id { get; init; }
    public Guid? ProductId { get; init; }
    public string? ProductTitle { get; init; }
    public string? ProductUnitOfMeasureTitle { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string? AssetNumber { get; init; }
    public Guid DestinationId { get; init; }
    public string DestinationTitle { get; init; } = string.Empty;
    public string ExitReason { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string DeliveringUnit { get; init; } = string.Empty;
    public DateTimeOffset? ReturnAtUtc { get; init; }
}

public sealed class GoodsIssueItemRowSummary : GoodsIssueItemSummary
{
    public Guid GoodsIssueId { get; init; }
    public string IssueNumber { get; init; } = string.Empty;
    public DateTimeOffset IssuedAtUtc { get; init; }
    public string IssuerFullName { get; init; } = string.Empty;
}

public sealed record GoodsIssueListResult(
    IReadOnlyCollection<GoodsIssueSummary> Items,
    int TotalCount,
    int FilteredCount,
    int PageNumber,
    int PageSize);

public sealed record GoodsIssueItemRowListResult(
    IReadOnlyCollection<GoodsIssueItemRowSummary> Items,
    int TotalCount,
    int FilteredCount,
    int PageNumber,
    int PageSize);

public sealed class GoodsIssueOptions
{
    public IReadOnlyCollection<PersonSummary> People { get; init; } = [];
}
