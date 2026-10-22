using System.Globalization;
using System.Text.Json;
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

    public async Task<GoodsReceiptItemHistoryResult?> GetGoodsReceiptItemHistoryAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.GoodsReceiptItems
            .AsNoTracking()
            .Where(row => row.Id == itemId)
            .Select(row => new
            {
                row.Id,
                row.GoodsReceiptId,
                row.ProductId,
                row.Product.Title,
                row.Product.Code
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (item is null) return null;

        var itemKey = item.Id.ToString();
        var receiptKey = item.GoodsReceiptId.ToString();
        var rows = await (
                from audit in dbContext.AuditTrails.AsNoTracking()
                join user in dbContext.Users.AsNoTracking()
                    on audit.UserId equals (Guid?)user.Id into matchedUsers
                from user in matchedUsers.DefaultIfEmpty()
                where
                    (audit.TableName == "GoodsReceiptItems" && audit.PrimaryKey.Contains(itemKey)) ||
                    (audit.TableName == "GoodsReceipts" && audit.PrimaryKey.Contains(receiptKey))
                orderby audit.OccurredAtUtc descending, audit.Id descending
                select new AuditTrailQueryRow
                {
                    Audit = audit,
                    UserFullName = audit.UserId == null
                        ? SystemActorName
                        : user != null && user.FullName != null && user.FullName != string.Empty
                            ? user.FullName
                            : MissingFullName
                })
            .ToArrayAsync(cancellationToken);

        var parsedRows = rows
            .Select(row => new ParsedAuditRow(
                row,
                ParseValues(row.Audit.OldValues),
                ParseValues(row.Audit.NewValues)))
            .ToArray();

        var productIds = ExtractIds(parsedRows, "ProductId").Append(item.ProductId).Distinct().ToArray();
        var warehouseIds = ExtractIds(parsedRows, "WarehouseId").ToArray();
        var supplierIds = ExtractIds(parsedRows, "SupplierId").ToArray();
        var delivererIds = ExtractIds(parsedRows, "DelivererId").ToArray();

        var products = await dbContext.Products.AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, product => product.Title, cancellationToken);
        var warehouses = await dbContext.Warehouses.AsNoTracking()
            .Where(warehouse => warehouseIds.Contains(warehouse.Id))
            .ToDictionaryAsync(warehouse => warehouse.Id, warehouse => warehouse.Title, cancellationToken);
        var suppliers = await dbContext.Suppliers.AsNoTracking()
            .Where(supplier => supplierIds.Contains(supplier.Id))
            .ToDictionaryAsync(supplier => supplier.Id, supplier => supplier.Title, cancellationToken);
        var people = await dbContext.People.AsNoTracking()
            .Where(person => delivererIds.Contains(person.Id))
            .ToDictionaryAsync(person => person.Id, person => person.FullName, cancellationToken);

        var lookups = new HistoryLookups(products, warehouses, suppliers, people);
        var entries = parsedRows
            .Select(row => ToHistoryEntry(row, lookups))
            .Where(entry => entry.Changes.Count > 0)
            .ToArray();

        return new GoodsReceiptItemHistoryResult(
            item.Id,
            $"{item.Title} ({item.Code})",
            entries);
    }

    private static EntityHistoryEntry ToHistoryEntry(
        ParsedAuditRow row,
        HistoryLookups lookups)
    {
        var definitions = row.Row.Audit.TableName == "GoodsReceipts"
            ? ReceiptFields
            : ReceiptItemFields;
        var changes = definitions
            .Where(field => row.OldValues.ContainsKey(field.Name) || row.NewValues.ContainsKey(field.Name))
            .Select(field => new EntityHistoryChange(
                field.Name,
                field.Label,
                FormatValue(field.Name, row.OldValues.GetValueOrDefault(field.Name), lookups),
                FormatValue(field.Name, row.NewValues.GetValueOrDefault(field.Name), lookups)))
            .ToArray();

        return new EntityHistoryEntry(
            row.Row.Audit.Id,
            row.Row.Audit.Action.ToString(),
            row.Row.Audit.Action switch
            {
                AuditAction.Create => "ثبت اولیه",
                AuditAction.Update => "ویرایش",
                AuditAction.Delete => "حذف",
                _ => row.Row.Audit.Action.ToString()
            },
            row.Row.Audit.TableName == "GoodsReceipts" ? "اطلاعات سند ورود" : "اطلاعات کالا",
            row.Row.UserFullName == SystemActorName ? "سیستم" : row.Row.UserFullName,
            row.Row.Audit.OccurredAtUtc,
            changes);
    }

    private static string FormatValue(
        string propertyName,
        JsonElement value,
        HistoryLookups lookups)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return "—";

        var raw = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
        if (string.IsNullOrWhiteSpace(raw)) return "—";

        if (Guid.TryParse(raw, out var id))
        {
            return propertyName switch
            {
                "ProductId" => lookups.Products.GetValueOrDefault(id, "کالای حذف‌شده"),
                "WarehouseId" => lookups.Warehouses.GetValueOrDefault(id, "انبار حذف‌شده"),
                "SupplierId" => lookups.Suppliers.GetValueOrDefault(id, "طرف حساب حذف‌شده"),
                "DelivererId" => lookups.People.GetValueOrDefault(id, "تحویل‌دهنده حذف‌شده"),
                _ => raw
            };
        }

        if (propertyName == "InvoiceStatus")
        {
            return raw switch
            {
                "HasInvoice" or "1" => "دارد",
                "DoesNotHave" or "0" => "ندارد",
                _ => "وارد نشده"
            };
        }

        if (propertyName == "EnteredAtUtc" &&
            DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date))
        {
            return date.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.GetCultureInfo("fa-IR"));
        }

        return raw;
    }

    private static Dictionary<string, JsonElement> ParseValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IEnumerable<Guid> ExtractIds(
        IEnumerable<ParsedAuditRow> rows,
        string propertyName) =>
        rows.SelectMany(row => new[]
            {
                row.OldValues.GetValueOrDefault(propertyName),
                row.NewValues.GetValueOrDefault(propertyName)
            })
            .Select(value => value.ValueKind == JsonValueKind.String ? value.GetString() : null)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct();

    private static readonly HistoryField[] ReceiptItemFields =
    [
        new("ProductId", "کالا"),
        new("Quantity", "تعداد"),
        new("WarehouseId", "انبار"),
        new("Description", "توضیحات کالا"),
        new("RequestNumber", "شماره درخواست"),
        new("InvoiceStatus", "وضعیت فاکتور"),
        new("InvoiceNumber", "شماره فاکتور"),
        new("SupplierId", "طرف حساب"),
        new("ReceiptNumber", "شماره رسید")
    ];

    private static readonly HistoryField[] ReceiptFields =
    [
        new("EnteredAtUtc", "تاریخ ورود"),
        new("PageNumber", "شماره صفحه"),
        new("DelivererId", "تحویل‌دهنده"),
        new("Description", "توضیحات ورودی")
    ];

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

    private sealed record ParsedAuditRow(
        AuditTrailQueryRow Row,
        Dictionary<string, JsonElement> OldValues,
        Dictionary<string, JsonElement> NewValues);

    private sealed record HistoryField(string Name, string Label);

    private sealed record HistoryLookups(
        IReadOnlyDictionary<Guid, string> Products,
        IReadOnlyDictionary<Guid, string> Warehouses,
        IReadOnlyDictionary<Guid, string> Suppliers,
        IReadOnlyDictionary<Guid, string> People);
}
