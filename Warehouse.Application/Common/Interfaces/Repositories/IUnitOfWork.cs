using Warehouse.Domain.Common;

namespace Warehouse.Application.Common.Interfaces.Repositories;

public interface IUnitOfWork<TId> : IDisposable, IAsyncDisposable
    where TId : notnull
{
    IRepositoryAsync<T, TId> Repository<T>()
        where T : Entity<TId>;

    Task<int> SaveAsync(CancellationToken cancellationToken = default);

    Task Rollback(CancellationToken cancellationToken = default);

    Task BeginTransaction(CancellationToken cancellationToken = default);

    Task BeginSerializableTransaction(CancellationToken cancellationToken = default) =>
        BeginTransaction(cancellationToken);

    Task CommitTransaction(CancellationToken cancellationToken = default);

    Task RollBackTransaction(CancellationToken cancellationToken = default);
}
