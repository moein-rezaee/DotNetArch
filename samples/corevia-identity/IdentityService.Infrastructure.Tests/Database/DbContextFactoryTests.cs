using IdentityService.Infrastructure.Database.Context;
using IdentityService.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IdentityService.Infrastructure.Tests.Database;

[Collection("env")]
public sealed class DbContextFactoryTests
{
    private static readonly string Pw = "pw-" + Guid.NewGuid().ToString("N");

    private static EnvScope CleanEnv() => new EnvScope()
        .Set("POSTGRES_HOST", null).Set("POSTGRES_PORT", null).Set("IDENTITY_POSTGRES_DB", null)
        .Set("POSTGRES_DB", null).Set("POSTGRES_USER", null).Set("POSTGRES_PASSWORD", null);

    [Fact]
    public void Design_time_factory_requires_the_password_and_has_no_hardcoded_fallback()
    {
        using var env = CleanEnv();

        var ex = Assert.Throws<InvalidOperationException>(() => new IdentityDbContextFactory().CreateDbContext([]));

        Assert.Contains("POSTGRES_PASSWORD", ex.Message);
    }

    [Fact]
    public void Blank_password_is_treated_as_missing()
    {
        using var env = CleanEnv().Set("POSTGRES_PASSWORD", "   ");

        Assert.Throws<InvalidOperationException>(() => new IdentityDbContextFactory().CreateDbContext([]));
    }

    [Fact]
    public void Other_keys_keep_their_historical_defaults()
    {
        using var env = CleanEnv().Set("POSTGRES_PASSWORD", Pw);

        using var context = new IdentityDbContextFactory().CreateDbContext([]);

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        var cs = new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());
        Assert.Equal(("localhost", 5432, "identitydb", "postgres", Pw), (cs.Host, cs.Port, cs.Database, cs.Username, cs.Password));
    }

    [Fact]
    public void Environment_overrides_and_identity_db_key_priority()
    {
        using var env = CleanEnv()
            .Set("POSTGRES_HOST", "db.internal").Set("POSTGRES_PORT", "6000")
            .Set("POSTGRES_DB", "generic").Set("IDENTITY_POSTGRES_DB", "identity-specific")
            .Set("POSTGRES_USER", "svc").Set("POSTGRES_PASSWORD", Pw);

        using var context = new IdentityDbContextFactory().CreateDbContext([]);

        var cs = new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());
        Assert.Equal(("db.internal", 6000, "identity-specific", "svc"), (cs.Host, cs.Port, cs.Database, cs.Username));
    }
}
