using System.Linq.Expressions;
using {{App}}.Application.Abstractions.Persistence;
using {{App}}.Application.Common.Pagination;
using {{App}}.Domain.Common;

namespace {{App}}.Application.Tests.Support;

/// <summary>In-memory unit of work for handler tests: no database, no mocking framework.</summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = new();

    public int SaveCount { get; private set; }

    public FakeRepository<TEntity> Fake<TEntity>()
        where TEntity : Entity =>
        (FakeRepository<TEntity>)Repository<TEntity>();

    public IRepository<TEntity> Repository<TEntity>()
        where TEntity : Entity
    {
        if (!_repositories.TryGetValue(typeof(TEntity), out var repository))
            _repositories[typeof(TEntity)] = repository = new FakeRepository<TEntity>();

        return (IRepository<TEntity>)repository;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}

internal sealed class FakeRepository<TEntity> : IRepository<TEntity>
    where TEntity : Entity
{
    public List<TEntity> Items { get; } = new();

    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(entity => entity.Id == id));

    public Task<PagedResult<TEntity>> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<TEntity, bool>>? filter = null, bool newestFirst = false, CancellationToken cancellationToken = default)
    {
        var query = Items.AsQueryable();
        if (filter is not null)
            query = query.Where(filter);

        var ordered = (newestFirst ? query.OrderByDescending(entity => entity.CreatedAtUtc).ThenByDescending(entity => entity.Id) : query.OrderBy(entity => entity.CreatedAtUtc).ThenBy(entity => entity.Id)).ToList();
        var page = ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResult<TEntity>(page, pageNumber, pageSize, ordered.Count));
    }

    public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.AsQueryable().FirstOrDefault(predicate));

    public Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TEntity>>(Items.AsQueryable().Where(predicate).OrderBy(entity => entity.CreatedAtUtc).ThenBy(entity => entity.Id).ToList());

    public Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(predicate is null ? Items.Count : Items.AsQueryable().Count(predicate));

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.AsQueryable().Any(predicate));

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Items.Remove(entity);
        return Task.CompletedTask;
    }
}
