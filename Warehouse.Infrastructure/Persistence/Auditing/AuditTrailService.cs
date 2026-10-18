using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Common.Interfaces;
using Warehouse.Application.Common.Models.Auditing;

namespace Warehouse.Infrastructure.Persistence.Auditing;

internal sealed class AuditTrailService(
    ApplicationDbContext dbContext) : IAuditTrailService
{
    private const string SystemActorName = "System";
    private const string MissingFullName = "نام کامل ثبت نشده";

    public async Task<GetAuditTrailsResult> GetAuditTrailsAsync(
        GetAuditTrailsRequest request,
        CancellationToken cancellationToken = default)
    {
        var query =
            from audit in dbContext.AuditTrails.AsNoTracking()
            join user in dbContext.Users.AsNoTracking()
                on audit.UserId equals (Guid?)user.Id into matchedUsers
            from user in matchedUsers.DefaultIfEmpty()
            select new AuditTrailQueryRow
            {
                Audit = audit,
                UserFullName = audit.UserId == null
                    ? SystemActorName
                    : user != null && user.FullName != null && user.FullName != string.Empty
                        ? user.FullName
                        : MissingFullName
            };

        var totalCount = await query.CountAsync(cancellationToken);
        var search = request.Search?.Trim();
        var userFullName = request.UserFullName?.Trim();
        var tableName = request.TableName?.Trim();
        var primaryKey = request.PrimaryKey?.Trim();
        var changedColumns = request.ChangedColumns?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var isUserId = Guid.TryParse(search, out var userId);
            var isAuditId = long.TryParse(search, out var auditId);
            var isAction = Enum.TryParse<AuditAction>(search, true, out var action);

            query = query.Where(row =>
                row.Audit.TableName.Contains(search) ||
                row.UserFullName.Contains(search) ||
                row.Audit.PrimaryKey.Contains(search) ||
                (row.Audit.ChangedColumns != null && row.Audit.ChangedColumns.Contains(search)) ||
                (isUserId && row.Audit.UserId == userId) ||
                (isAuditId && row.Audit.Id == auditId) ||
                (isAction && row.Audit.Action == action));
        }

        if (!string.IsNullOrWhiteSpace(userFullName))
        {
            query = query.Where(row => row.UserFullName.Contains(userFullName));
        }

        if (!string.IsNullOrWhiteSpace(request.Action) &&
            Enum.TryParse<AuditAction>(request.Action, true, out var filteredAction))
        {
            query = query.Where(row => row.Audit.Action == filteredAction);
        }

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            query = query.Where(row => row.Audit.TableName.Contains(tableName));
        }

        if (!string.IsNullOrWhiteSpace(primaryKey))
        {
            query = query.Where(row => row.Audit.PrimaryKey.Contains(primaryKey));
        }

        if (!string.IsNullOrWhiteSpace(changedColumns))
        {
            query = query.Where(row =>
                row.Audit.ChangedColumns != null &&
                row.Audit.ChangedColumns.Contains(changedColumns));
        }

        var filteredCount = await query.CountAsync(cancellationToken);
        query = ApplySorting(query, request.SortBy, request.SortDescending);

        var pagedQuery = request.PaginationEnabled
            ? query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
            : query;
        var page = await pagedQuery.ToArrayAsync(cancellationToken);
        var items = page
            .Select(row => new AuditTrailSummary(
                row.Audit.Id,
                row.Audit.UserId,
                row.UserFullName,
                row.Audit.Action.ToString(),
                row.Audit.TableName,
                row.Audit.PrimaryKey,
                row.Audit.OldValues,
                row.Audit.NewValues,
                row.Audit.ChangedColumns,
                row.Audit.OccurredAtUtc))
            .ToArray();

        return new GetAuditTrailsResult(
            items,
            totalCount,
            filteredCount,
            request.PaginationEnabled ? request.PageNumber : 1,
            request.PaginationEnabled ? request.PageSize : items.Length,
            request.PaginationEnabled);
    }

    private static IQueryable<AuditTrailQueryRow> ApplySorting(
        IQueryable<AuditTrailQueryRow> query,
        string? sortBy,
        bool descending) =>
        (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("id", false) => query.OrderBy(row => row.Audit.Id),
            ("id", true) => query.OrderByDescending(row => row.Audit.Id),
            ("userfullname", false) => query.OrderBy(row => row.UserFullName).ThenBy(row => row.Audit.Id),
            ("userfullname", true) => query.OrderByDescending(row => row.UserFullName).ThenByDescending(row => row.Audit.Id),
            ("action", false) => query.OrderBy(row => row.Audit.Action).ThenBy(row => row.Audit.Id),
            ("action", true) => query.OrderByDescending(row => row.Audit.Action).ThenByDescending(row => row.Audit.Id),
            ("tablename", false) => query.OrderBy(row => row.Audit.TableName).ThenBy(row => row.Audit.Id),
            ("tablename", true) => query.OrderByDescending(row => row.Audit.TableName).ThenByDescending(row => row.Audit.Id),
            ("occurredatutc", false) => query.OrderBy(row => row.Audit.OccurredAtUtc).ThenBy(row => row.Audit.Id),
            _ => query.OrderByDescending(row => row.Audit.OccurredAtUtc).ThenByDescending(row => row.Audit.Id)
        };

    private sealed class AuditTrailQueryRow
    {
        public required AuditTrail Audit { get; init; }

        public required string UserFullName { get; init; }
    }
}
