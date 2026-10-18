using Warehouse.Server.Contracts.Common;

namespace Warehouse.Server.Contracts.Auditing;

public sealed class GetAuditLogsRequest : DataTableRequest
{
    public GetAuditLogsRequest()
    {
        SortDescending = true;
    }

    public string? UserFullName { get; init; }

    public string? Action { get; init; }

    public string? TableName { get; init; }

    public string? PrimaryKey { get; init; }

    public string? ChangedColumns { get; init; }
}
