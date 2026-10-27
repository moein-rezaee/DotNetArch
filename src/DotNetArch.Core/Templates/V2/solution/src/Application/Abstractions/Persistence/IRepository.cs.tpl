using System.Linq.Expressions;
using {{App}}.Application.Common.Pagination;
using {{App}}.Domain.Common;

namespace {{App}}.Application.Abstractions.Persistence;

/// <summary>Port for aggregate persistence. Returns materialised results only: no query object leaks out of the adapter.</summary>
public interface IRepository<TEntity>
    where TEntity : Entity
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<TEntity>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken cancellationToken = default,
        bool newestFirst = false);

    /// <summary>How many entities match (all of them without a predicate).</summary>
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>The first entity that matches, tracked so changes made to it are saved with the unit of work; null when none matches.</summary>
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>Every entity that matches, tracked. Use it for small sets (the children of one aggregate); page large sets with <see cref="GetPagedAsync"/>.</summary>
    Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default);
}
