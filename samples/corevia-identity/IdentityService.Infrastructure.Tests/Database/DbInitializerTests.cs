using System.Net;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Database;
using IdentityService.Infrastructure.Database.Context;
using IdentityService.Infrastructure.Database.Initialization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace IdentityService.Infrastructure.Tests.Database;

/// <summary>
/// DbInitializer branch selection and dependency-failure behaviour for each provider, against a closed local port
/// (connection refused, no server needed). Success paths (create database, migrate, seed) need a real server and are
/// listed as a follow-up integration suite in docs/specs/testspec.yaml.
/// </summary>
public sealed class DbInitializerTests
{
    private static readonly string Pw = "pw-" + Guid.NewGuid().ToString("N");

    private sealed class RecordingSeed : IIdentitySeedService
    {
        public int Calls { get; private set; }

        public Task SeedAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private static (DbInitializer Initializer, RecordingSeed Seed) Create(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var sp = new ServiceCollection().AddIdentityDatabase(configuration).BuildServiceProvider();
        var seed = new RecordingSeed();
        return (new DbInitializer(sp.GetRequiredService<IdentityDbContext>(), seed, NullLogger<DbInitializer>.Instance), seed);
    }

    [Fact]
    public async Task Postgres_branch_with_an_unreachable_server_fails_with_ExternalServiceException_wrapping_NpgsqlException_and_never_seeds()
    {
        var (initializer, seed) = Create(new()
        {
            ["DB_PROVIDER"] = "Postgres",
            ["POSTGRES_HOST"] = "127.0.0.1",
            ["POSTGRES_PORT"] = "1",
            ["POSTGRES_PASSWORD"] = Pw
        });

        var ex = await Assert.ThrowsAsync<ExternalServiceException>(() => initializer.InitializeAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Equal("database_unavailable", ex.ErrorCode);
        Assert.IsAssignableFrom<NpgsqlException>(ex.InnerException);
        Assert.Equal(0, seed.Calls);
    }

    [Fact]
    public async Task SqlServer_branch_with_an_unreachable_server_fails_with_ExternalServiceException_wrapping_SqlException_and_never_seeds()
    {
        var (initializer, seed) = Create(new()
        {
            ["DB_PROVIDER"] = "SqlServer",
            ["SQLSERVER_HOST"] = "127.0.0.1",
            ["SQLSERVER_PORT"] = "1",
            ["SQLSERVER_USER"] = "sa",
            ["SQLSERVER_PASSWORD"] = Pw
        });

        var ex = await Assert.ThrowsAsync<ExternalServiceException>(() => initializer.InitializeAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Equal("database_unavailable", ex.ErrorCode);
        Assert.IsType<SqlException>(ex.InnerException);
        Assert.Equal(0, seed.Calls);
    }

    [Fact]
    public async Task Cancellation_is_honoured_on_the_Postgres_branch()
    {
        var (initializer, _) = Create(new() { ["POSTGRES_HOST"] = "127.0.0.1", ["POSTGRES_PORT"] = "1", ["POSTGRES_PASSWORD"] = Pw });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initializer.InitializeAsync(cts.Token));
    }
}
