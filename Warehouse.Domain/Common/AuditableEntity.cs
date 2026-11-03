namespace Warehouse.Domain.Common;

public abstract class AuditableEntity
{
    public DateTimeOffset? CreatedOn { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? LastModifiedOn { get; private set; }

    public string? LastModifiedBy { get; private set; }
}
