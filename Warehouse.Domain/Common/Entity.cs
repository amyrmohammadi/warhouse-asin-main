namespace Warehouse.Domain.Common;

public abstract class Entity<TId> : AuditableEntity
    where TId : notnull
{
    public TId Id { get; protected init; } = default!;
}
