namespace Warehouse.Domain.Common;

public abstract class ConcurrencyEntity<TId> : Entity<TId>
    where TId : notnull
{
    public Guid ConcurrencyStamp { get; private set; } = Guid.NewGuid();

    public bool TryAdvanceConcurrencyStamp(Guid expectedStamp)
    {
        if (expectedStamp == Guid.Empty || ConcurrencyStamp != expectedStamp)
        {
            return false;
        }

        ConcurrencyStamp = Guid.NewGuid();
        return true;
    }
}
