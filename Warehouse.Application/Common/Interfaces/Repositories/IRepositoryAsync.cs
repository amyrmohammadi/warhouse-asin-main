using Warehouse.Domain.Common;

namespace Warehouse.Application.Common.Interfaces.Repositories;

public interface IRepositoryAsync<T, in TId>
    where T : Entity<TId>
    where TId : notnull
{
    IQueryable<T> Entities { get; }

    Task<T?> GetByIdAsync(
        TId id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetPagedResponseAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        IQueryable<T> query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> ToListAsync(
        IQueryable<T> query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TResult>> ToListAsync<TResult>(
        IQueryable<TResult> query,
        CancellationToken cancellationToken = default);

    Task<T> AddAsync(
        T entity,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        T entity,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        T entity,
        CancellationToken cancellationToken = default);
}
