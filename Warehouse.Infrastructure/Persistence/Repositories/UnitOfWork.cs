using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Domain.Common;

namespace Warehouse.Infrastructure.Persistence.Repositories;

internal sealed class UnitOfWork<TId>(ApplicationDbContext dbContext)
    : IUnitOfWork<TId>
    where TId : notnull
{
    private readonly Dictionary<Type, object> _repositories = [];
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    public IRepositoryAsync<T, TId> Repository<T>()
        where T : Entity<TId>
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_repositories.TryGetValue(typeof(T), out var repository))
        {
            return (IRepositoryAsync<T, TId>)repository;
        }

        var createdRepository = new RepositoryAsync<T, TId>(dbContext);
        _repositories.Add(typeof(T), createdRepository);
        return createdRepository;
    }

    public Task<int> SaveAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task Rollback(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        foreach (var entry in dbContext.ChangeTracker.Entries().ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.State == EntityState.Added)
            {
                entry.State = EntityState.Detached;
                continue;
            }

            await entry.ReloadAsync(cancellationToken);
        }
    }

    public async Task BeginTransaction(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (dbContext.Database.CurrentTransaction is not null)
        {
            return;
        }

        _transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task BeginSerializableTransaction(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (dbContext.Database.CurrentTransaction is not null) return;
        _transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
    }

    public async Task CommitTransaction(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_transaction is null)
        {
            return;
        }

        await _transaction.CommitAsync(cancellationToken);
        await DisposeTransactionAsync();
    }

    public async Task RollBackTransaction(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await DisposeTransactionAsync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _transaction?.Dispose();
        _transaction = null;
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await DisposeTransactionAsync();
        _disposed = true;
    }

    private async ValueTask DisposeTransactionAsync()
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.DisposeAsync();
        _transaction = null;
    }
}
