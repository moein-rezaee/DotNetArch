using System.Linq.Expressions;
using {{App}}.Application.Abstractions.Persistence;
using {{App}}.Application.Common.Pagination;
using {{App}}.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace {{App}}.Infrastructure.Persistence.Repositories;

internal sealed class EfRepository<TEntity>(AppDbContext context) : IRepository<TEntity>
    where TEntity : Entity
{
    private readonly DbSet<TEntity> _set = context.Set<TEntity>();

    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _set.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task<PagedResult<TEntity>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken cancellationToken = default,
        bool newestFirst = false)
    {
        var query = _set.AsNoTracking();
        if (filter is not null)
            query = query.Where(filter);

        var total = await query.CountAsync(cancellationToken);
        var items = await (newestFirst ? query.OrderByDescending(entity => entity.CreatedAtUtc).ThenByDescending(entity => entity.Id) : query.OrderBy(entity => entity.CreatedAtUtc).ThenBy(entity => entity.Id))
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TEntity>(items, pageNumber, pageSize, total);
    }

    public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        _set.FirstOrDefaultAsync(predicate, cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        await _set.Where(predicate).OrderBy(entity => entity.CreatedAtUtc).ThenBy(entity => entity.Id).ToListAsync(cancellationToken);

    public Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default) =>
        predicate is null ? _set.CountAsync(cancellationToken) : _set.CountAsync(predicate, cancellationToken);

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        _set.AnyAsync(predicate, cancellationToken);

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        await _set.AddAsync(entity, cancellationToken);

    public Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _set.Remove(entity);
        return Task.CompletedTask;
    }
}
