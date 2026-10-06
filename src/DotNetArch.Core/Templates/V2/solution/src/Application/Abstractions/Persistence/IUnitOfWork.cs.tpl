using {{App}}.Domain.Common;

namespace {{App}}.Application.Abstractions.Persistence;

/// <summary>Coordinates repositories and commits their changes atomically; domain events are dispatched after a successful commit.</summary>
public interface IUnitOfWork
{
    IRepository<TEntity> Repository<TEntity>()
        where TEntity : Entity;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
