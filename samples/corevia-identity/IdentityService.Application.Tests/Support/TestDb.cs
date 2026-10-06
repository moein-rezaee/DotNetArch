using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Database.Context;
using IdentityService.Infrastructure.Database.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Application.Tests.Support;

/// <summary>
/// Real EfRepository/UnitOfWork over a private SQLite in-memory database. Used only so Application handlers run
/// their real LINQ against a relational store; it makes no claim about Postgres or SQL Server behaviour.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    private TestDb(SqliteConnection connection, IdentityDbContext context)
    {
        _connection = connection;
        Context = context;
        Uow = new UnitOfWork(context);
    }

    public IdentityDbContext Context { get; }

    public IUnitOfWork Uow { get; }

    public static TestDb Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection).Options;
        var context = new IdentityDbContext(options);
        context.Database.EnsureCreated();
        return new TestDb(connection, context);
    }

    /// <summary>A second context/UoW on the same database, to assert persisted state rather than tracked entities.</summary>
    public IdentityDbContext NewContext()
        => new(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(_connection).Options);

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
