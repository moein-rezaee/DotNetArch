using {{App}}.Application.Abstractions.Messaging;
using {{App}}.Application.Abstractions.Persistence;
using {{App}}.Domain.Common;

namespace {{App}}.Infrastructure.Persistence.Repositories;

internal sealed class UnitOfWork(AppDbContext context, IDomainEventDispatcher dispatcher) : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = new();

    public IRepository<TEntity> Repository<TEntity>()
        where TEntity : Entity
    {
        if (_repositories.TryGetValue(typeof(TEntity), out var existing))
            return (IRepository<TEntity>)existing;

        var repository = new EfRepository<TEntity>(context);
        _repositories[typeof(TEntity)] = repository;
        return repository;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entities = context.ChangeTracker.Entries<Entity>().Select(entry => entry.Entity).ToList();
        var domainEvents = entities.SelectMany(entity => entity.DomainEvents).ToList();
        entities.ForEach(entity => entity.ClearDomainEvents());

        var changes = await context.SaveChangesAsync(cancellationToken);
        await dispatcher.DispatchAsync(domainEvents, cancellationToken);
        return changes;
    }
}
