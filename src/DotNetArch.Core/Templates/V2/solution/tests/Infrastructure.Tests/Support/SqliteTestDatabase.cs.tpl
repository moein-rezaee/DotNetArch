using {{App}}.Application.Abstractions.Messaging;
using {{App}}.Domain.Common;
using {{App}}.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace {{App}}.Infrastructure.Tests.Support;

/// <summary>
/// A throw-away in-memory SQLite database with the real EF model. The model is provider-agnostic, so repository behaviour
/// is verified here even when the service itself runs on SQL Server or PostgreSQL.
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteTestDatabase()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}

internal sealed class RecordingDispatcher : IDomainEventDispatcher
{
    public List<IDomainEvent> Dispatched { get; } = new();

    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        Dispatched.AddRange(domainEvents);
        return Task.CompletedTask;
    }
}
