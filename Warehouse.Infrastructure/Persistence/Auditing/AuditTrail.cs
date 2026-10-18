namespace Warehouse.Infrastructure.Persistence.Auditing;

public sealed class AuditTrail
{
    public long Id { get; set; }

    public Guid? UserId { get; set; }

    public string UserName { get; set; } = "System";

    public AuditAction Action { get; set; }

    public string TableName { get; set; } = string.Empty;

    public string PrimaryKey { get; set; } = string.Empty;

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? ChangedColumns { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}
