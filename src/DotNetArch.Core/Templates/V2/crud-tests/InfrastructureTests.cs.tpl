using {{App}}.Domain.Entities;
using {{App}}.Infrastructure.Persistence.Repositories;
using {{App}}.Infrastructure.Tests.Support;

namespace {{App}}.Infrastructure.Tests.Persistence;

public sealed class {{Entity}}PersistenceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Added_entities_survive_a_round_trip_through_the_database()
    {
        var entity = {{Entity}}.Create("Widget", Now);
        await using (var write = _database.CreateContext())
        {
            var unitOfWork = new UnitOfWork(write, new RecordingDispatcher());
            await unitOfWork.Repository<{{Entity}}>().AddAsync(entity);
            await unitOfWork.SaveChangesAsync();
        }

        await using var read = _database.CreateContext();
        var loaded = await new EfRepository<{{Entity}}>(read).GetByIdAsync(entity.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Widget", loaded.Name);
    }

    [Fact]
    public async Task Paging_is_ordered_and_counts_the_total()
    {
        await using (var write = _database.CreateContext())
        {
            for (var index = 0; index < 5; index++)
                write.Add({{Entity}}.Create($"Item {index}", Now.AddMinutes(index)));
            await write.SaveChangesAsync();
        }

        await using var read = _database.CreateContext();
        var page = await new EfRepository<{{Entity}}>(read).GetPagedAsync(pageNumber: 2, pageSize: 2);

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(new[] { "Item 2", "Item 3" }, page.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task Removed_entities_are_gone_after_save()
    {
        var entity = {{Entity}}.Create("Temp", Now);
        await using (var write = _database.CreateContext())
        {
            write.Add(entity);
            await write.SaveChangesAsync();
        }

        await using (var delete = _database.CreateContext())
        {
            var unitOfWork = new UnitOfWork(delete, new RecordingDispatcher());
            var repository = unitOfWork.Repository<{{Entity}}>();
            await repository.RemoveAsync((await repository.GetByIdAsync(entity.Id))!);
            await unitOfWork.SaveChangesAsync();
        }

        await using var read = _database.CreateContext();
        Assert.Null(await new EfRepository<{{Entity}}>(read).GetByIdAsync(entity.Id));
    }
}
