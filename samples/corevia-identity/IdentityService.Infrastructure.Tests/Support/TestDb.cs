using IdentityService.Infrastructure.Database.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Tests.Support;

/// <summary>SQLite in-memory relational stand-in for repository/UnitOfWork mechanics only (no provider claims).</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TestDb()
    {
        _connection.Open();
        Context = New();
        Context.Database.EnsureCreated();
    }

    public IdentityDbContext Context { get; }

    public IdentityDbContext New()
        => new(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(_connection).Options);

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
