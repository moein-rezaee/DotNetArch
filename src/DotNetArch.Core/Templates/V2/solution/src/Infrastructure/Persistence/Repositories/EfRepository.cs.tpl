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
        CancellationToken cancellationToken = default)
    {
        var query = _set.AsNoTracking();
        if (filter is not null)
            query = query.Where(filter);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(entity => entity.CreatedAtUtc)
            .ThenBy(entity => entity.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TEntity>(items, pageNumber, pageSize, total);
    }

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        _set.AnyAsync(predicate, cancellationToken);

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        await _set.AddAsync(entity, cancellationToken);

    public void Remove(TEntity entity) => _set.Remove(entity);
}
