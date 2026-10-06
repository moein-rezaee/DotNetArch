using System.Linq.Expressions;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Database.Repositories;

public sealed class EfRepository<T> : IRepository<T> where T : class
{
    private readonly IdentityDbContext _context;
    private readonly DbSet<T> _set;

    public EfRepository(IdentityDbContext context)
    {
        _context = context;
        _set = _context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return await _set.FindAsync([id], cancellationToken);
    }

    public async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _set.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _set.AddAsync(entity, cancellationToken);
    }

    public void Remove(T entity)
    {
        _set.Remove(entity);
    }

    public IQueryable<T> Query()
    {
        return _set;
    }
}
