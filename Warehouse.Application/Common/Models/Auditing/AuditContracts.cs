using Warehouse.Application.Common.Models.Filtering;

namespace Warehouse.Application.Common.Models.Auditing;

public sealed record GetAuditTrailsRequest : DataTableParameters
{
    public GetAuditTrailsRequest()
    {
        SortDescending = true;
    }

    public string? UserFullName { get; init; }

    public string? Action { get; init; }

    public string? TableName { get; init; }

    public string? PrimaryKey { get; init; }

    public string? ChangedColumns { get; init; }
}

public sealed record GetAuditTrailsResult(
    IReadOnlyCollection<AuditTrailSummary> Items,
    int TotalCount,
    int FilteredCount,
    int PageNumber,
    int PageSize,
    bool PaginationEnabled);

public sealed record AuditTrailSummary(
    long Id,
    Guid? UserId,
    string UserFullName,
    string Action,
    string TableName,
    string PrimaryKey,
    string? OldValues,
    string? NewValues,
    string? ChangedColumns,
    DateTimeOffset OccurredAtUtc);

public sealed record EntityHistoryChange(
    string FieldName,
    string FieldLabel,
    string OldValue,
    string NewValue);

public sealed record EntityHistoryEntry(
    long Id,
    string Action,
    string ActionLabel,
    string SectionLabel,
    string UserFullName,
    DateTimeOffset OccurredAtUtc,
    IReadOnlyCollection<EntityHistoryChange> Changes);

public sealed record GoodsReceiptItemHistoryResult(
    Guid ItemId,
    string ItemTitle,
    IReadOnlyCollection<EntityHistoryEntry> Entries);
