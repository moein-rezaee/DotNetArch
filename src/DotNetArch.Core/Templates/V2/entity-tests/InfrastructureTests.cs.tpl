using {{App}}.Domain.Entities;
using {{App}}.Infrastructure.Persistence.Repositories;
using {{App}}.Infrastructure.Tests.Support;
{{EfUsing}}{{EnumUsing}}
namespace {{App}}.Infrastructure.Tests.Persistence;

public sealed class {{Entity}}PersistenceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Saved_entities_survive_a_round_trip_through_the_database()
    {
        var entity = {{Entity}}.Create({{SampleArguments}});
        await using (var write = _database.CreateContext())
        {
            var unitOfWork = new UnitOfWork(write, new RecordingDispatcher());
            await unitOfWork.Repository<{{Entity}}>().AddAsync(entity);
            await unitOfWork.SaveChangesAsync();
        }

        await using var read = _database.CreateContext();
        var loaded = await new EfRepository<{{Entity}}>(read).FirstOrDefaultAsync(item => item.Id == entity.Id);

        Assert.NotNull(loaded);
{{LoadedAssertions}}
    }
{{UniqueTests}}}
