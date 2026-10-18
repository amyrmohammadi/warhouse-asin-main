using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Common.Interfaces.Repositories;
using Warehouse.Domain.Common;

namespace Warehouse.Infrastructure.Persistence.Repositories;

internal sealed class RepositoryAsync<T, TId>(ApplicationDbContext dbContext)
    : IRepositoryAsync<T, TId>
    where T : Entity<TId>
    where TId : notnull
{
    public IQueryable<T> Entities => dbContext.Set<T>().AsNoTracking();

    public async Task<T?> GetByIdAsync(
        TId id,
        CancellationToken cancellationToken = default) =>
        await dbContext.Set<T>().FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<T>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        await Entities.ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<T>> GetPagedResponseAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        await Entities
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(
        IQueryable<T> query,
        CancellationToken cancellationToken = default) =>
        query.CountAsync(cancellationToken);

    public async Task<IReadOnlyList<T>> ToListAsync(
        IQueryable<T> query,
        CancellationToken cancellationToken = default) =>
        await query.ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TResult>> ToListAsync<TResult>(
        IQueryable<TResult> query,
        CancellationToken cancellationToken = default) =>
        await query.ToListAsync(cancellationToken);

    public async Task<T> AddAsync(
        T entity,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Set<T>().AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task UpdateAsync(
        T entity,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        dbContext.Set<T>().Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        T entity,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        dbContext.Set<T>().Remove(entity);
        return Task.CompletedTask;
    }
}
