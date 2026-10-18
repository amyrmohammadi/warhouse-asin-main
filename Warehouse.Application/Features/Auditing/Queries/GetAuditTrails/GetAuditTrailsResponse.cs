namespace Warehouse.Application.Features.Auditing.Queries.GetAuditTrails;

public sealed record GetAuditTrailsResponse(
    IReadOnlyCollection<AuditTrailListItemResponse> Items,
    int TotalCount,
    int FilteredCount,
    int PageNumber,
    int PageSize,
    bool PaginationEnabled);

public sealed record AuditTrailListItemResponse(
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
