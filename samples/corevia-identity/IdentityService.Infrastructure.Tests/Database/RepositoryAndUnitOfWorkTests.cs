using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Database.Repositories;
using IdentityService.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Tests.Database;

public sealed class RepositoryAndUnitOfWorkTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private static User NewUser(string phone) => new() { Id = Guid.NewGuid(), PhoneNumber = phone, CreatedAt = DateTime.UtcNow };

    [Fact]
    public async Task AddAsync_is_not_persisted_until_SaveChanges()
    {
        var uow = new UnitOfWork(_db.Context);
        await uow.Repository<User>().AddAsync(NewUser("09120000001"));

        Assert.Equal(0, await _db.New().Users.CountAsync());
        Assert.Equal(1, await uow.SaveChangesAsync());
        Assert.Equal(1, await _db.New().Users.CountAsync());
    }

    [Fact]
    public async Task GetById_and_FirstOrDefault_find_or_return_null()
    {
        var uow = new UnitOfWork(_db.Context);
        var user = NewUser("09120000001");
        await uow.Repository<User>().AddAsync(user);
        await uow.SaveChangesAsync();
        var repo = new UnitOfWork(_db.New()).Repository<User>();

        Assert.Equal(user.PhoneNumber, (await repo.GetByIdAsync(user.Id))!.PhoneNumber);
        Assert.Null(await repo.GetByIdAsync(Guid.NewGuid()));
        Assert.Equal(user.Id, (await repo.FirstOrDefaultAsync(u => u.PhoneNumber == "09120000001"))!.Id);
        Assert.Null(await repo.FirstOrDefaultAsync(u => u.PhoneNumber == "nope"));
    }

    [Fact]
    public async Task Remove_deletes_after_SaveChanges()
    {
        var uow = new UnitOfWork(_db.Context);
        var user = NewUser("09120000001");
        await uow.Repository<User>().AddAsync(user);
        await uow.SaveChangesAsync();

        uow.Repository<User>().Remove(user);
        await uow.SaveChangesAsync();

        Assert.Empty(await _db.New().Users.ToListAsync());
    }

    [Fact]
    public async Task Query_is_composable_IQueryable()
    {
        var uow = new UnitOfWork(_db.Context);
        await uow.Repository<User>().AddAsync(NewUser("09120000001"));
        await uow.Repository<User>().AddAsync(NewUser("09120000002"));
        await uow.SaveChangesAsync();

        var phones = uow.Repository<User>().Query().OrderByDescending(u => u.PhoneNumber).Select(u => u.PhoneNumber).ToList();

        Assert.Equal(new[] { "09120000002", "09120000001" }, phones);
    }

    [Fact]
    public void UnitOfWork_caches_one_repository_per_entity_type()
    {
        var uow = new UnitOfWork(_db.Context);

        Assert.Same(uow.Repository<User>(), uow.Repository<User>());
        Assert.NotSame((object)uow.Repository<User>(), uow.Repository<Role>());
        Assert.IsType<EfRepository<User>>(uow.Repository<User>());
    }

    [Fact]
    public async Task Unique_phone_index_violation_surfaces_as_DbUpdateException()
    {
        var uow = new UnitOfWork(_db.Context);
        await uow.Repository<User>().AddAsync(NewUser("09120000001"));
        await uow.SaveChangesAsync();

        await uow.Repository<User>().AddAsync(NewUser("09120000001"));

        await Assert.ThrowsAsync<DbUpdateException>(() => uow.SaveChangesAsync());
    }

    [Fact]
    public async Task Cancelled_token_cancels_SaveChanges()
    {
        var uow = new UnitOfWork(_db.Context);
        await uow.Repository<User>().AddAsync(NewUser("09120000001"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => uow.SaveChangesAsync(cts.Token));
    }
}
