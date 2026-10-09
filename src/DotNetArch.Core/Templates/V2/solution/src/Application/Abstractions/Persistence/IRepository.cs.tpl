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
        CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default);
}
