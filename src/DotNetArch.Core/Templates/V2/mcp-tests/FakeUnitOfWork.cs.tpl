using System.Linq.Expressions;
using {{App}}.Application.Abstractions.Persistence;
using {{App}}.Application.Common.Pagination;
using {{App}}.Domain.Common;

namespace {{App}}.Mcp.Tests.Support;

/// <summary>In-memory unit of work for tool tests: no database needed.</summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = new();

    public IRepository<TEntity> Repository<TEntity>()
        where TEntity : Entity
    {
        if (!_repositories.TryGetValue(typeof(TEntity), out var repository))
            _repositories[typeof(TEntity)] = repository = new FakeRepository<TEntity>();

        return (IRepository<TEntity>)repository;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
}

internal sealed class FakeRepository<TEntity> : IRepository<TEntity>
    where TEntity : Entity
{
    private readonly List<TEntity> _items = new();

    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.FirstOrDefault(entity => entity.Id == id));

    public Task<PagedResult<TEntity>> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<TEntity, bool>>? filter = null, CancellationToken cancellationToken = default)
    {
        var query = _items.AsQueryable();
        if (filter is not null)
            query = query.Where(filter);

        var ordered = query.OrderBy(entity => entity.CreatedAtUtc).ThenBy(entity => entity.Id).ToList();
        return Task.FromResult(new PagedResult<TEntity>(ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(), pageNumber, pageSize, ordered.Count));
    }

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.AsQueryable().Any(predicate));

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _items.Add(entity);
        return Task.CompletedTask;
    }

    public void Remove(TEntity entity) => _items.Remove(entity);
}
