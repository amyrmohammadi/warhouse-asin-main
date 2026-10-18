using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Warehouse.Infrastructure.Persistence.Auditing;

internal sealed class AuditEntry(EntityEntry entry)
{
    private const string RedactedValue = "[REDACTED]";

    private static readonly string[] SensitivePropertyFragments =
    [
        "Password",
        "Token",
        "Stamp",
        "Secret",
        "Authenticator",
        "Image"
    ];

    public EntityEntry Entry { get; } = entry;

    public Guid? UserId { get; init; }

    public string UserName { get; init; } = "System";

    public AuditAction Action { get; init; }

    public string TableName { get; init; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; init; }

    public Dictionary<string, object?> KeyValues { get; } = [];

    public Dictionary<string, object?> OldValues { get; } = [];

    public Dictionary<string, object?> NewValues { get; } = [];

    public List<string> ChangedColumns { get; } = [];

    public List<PropertyEntry> TemporaryProperties { get; } = [];

    public bool HasTemporaryProperties => TemporaryProperties.Count > 0;

    public object? Sanitize(string propertyName, object? value)
    {
        if (value is null)
        {
            return null;
        }

        var isSensitive = SensitivePropertyFragments.Any(fragment =>
            propertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        var isIdentityTokenValue =
            string.Equals(TableName, "UserTokens", StringComparison.Ordinal) &&
            string.Equals(propertyName, "Value", StringComparison.Ordinal);

        return isSensitive || isIdentityTokenValue ? RedactedValue : value;
    }

    public AuditTrail ToAuditTrail() => new()
    {
        UserId = UserId,
        UserName = UserName,
        Action = Action,
        TableName = TableName,
        PrimaryKey = JsonSerializer.Serialize(KeyValues),
        OldValues = SerializeOrNull(OldValues),
        NewValues = SerializeOrNull(NewValues),
        ChangedColumns = ChangedColumns.Count == 0
            ? null
            : JsonSerializer.Serialize(ChangedColumns),
        OccurredAtUtc = OccurredAtUtc
    };

    private static string? SerializeOrNull(Dictionary<string, object?> values) =>
        values.Count == 0 ? null : JsonSerializer.Serialize(values);
}
