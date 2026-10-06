using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Database.Context;

namespace IdentityService.Infrastructure.Database.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly IdentityDbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(IdentityDbContext context)
    {
        _context = context;
    }

    public IRepository<T> Repository<T>() where T : class
    {
        if (_repositories.TryGetValue(typeof(T), out var repository))
        {
            return (IRepository<T>)repository;
        }

        var newRepository = new EfRepository<T>(_context);
        _repositories[typeof(T)] = newRepository;

        return newRepository;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}

