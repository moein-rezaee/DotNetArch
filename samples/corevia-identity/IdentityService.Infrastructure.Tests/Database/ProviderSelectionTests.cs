using Corevia.Kit.DatabaseConnection.Core;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Database;
using IdentityService.Infrastructure.Database.Context;
using IdentityService.Infrastructure.Database.Initialization;
using IdentityService.Infrastructure.Database.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IdentityService.Infrastructure.Tests.Database;

/// <summary>
/// Own-storage Port provider selection. Uses the real Kit DatabaseConnection wiring with the real Npgsql and
/// SqlServer EF providers; nothing connects (DbContext construction and GetConnectionString are offline).
/// </summary>
public sealed class ProviderSelectionTests
{
    private const string NpgsqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    // Generated per run so no credential-looking literal lives in the repository.
    private static readonly string Pw = "pw-" + Guid.NewGuid().ToString("N");

    // Passwords are secrets with no default (see the missing-password tests). Unless a test supplies its own
    // password key, give both engines a generated one so the tests below exercise only what they name.
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        if (!settings.Keys.Any(k => k.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase)))
        {
            settings["POSTGRES_PASSWORD"] = Pw;
            settings["SQLSERVER_PASSWORD"] = Pw;
        }

        return BuildWithoutDefaults(settings);
    }

    private static ServiceProvider BuildWithoutDefaults(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddIdentityDatabase(configuration).BuildServiceProvider();
    }

    private static IdentityDbContext Context(ServiceProvider sp) => sp.CreateScope().ServiceProvider.GetRequiredService<IdentityDbContext>();

    // ---------------------------------------------------------------- Postgres branch
    [Fact]
    public void Postgres_is_the_default_provider_with_historical_defaults()
    {
        using var sp = Build(new() { ["POSTGRES_PASSWORD"] = Pw });
        using var context = Context(sp);

        Assert.Equal(NpgsqlProvider, context.Database.ProviderName);
        var cs = new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());
        Assert.Equal("localhost", cs.Host);
        Assert.Equal(5432, cs.Port);
        Assert.Equal("identitydb", cs.Database);
        Assert.Equal("postgres", cs.Username);
        Assert.Equal(Pw, cs.Password);
    }

    [Theory]
    [InlineData("IdentityDb:Provider", "Postgres")]
    [InlineData("IdentityDb:Provider", "postgresql")]
    [InlineData("DB_PROVIDER", "Postgres")]
    public void Postgres_can_be_selected_explicitly_through_either_provider_key(string key, string value)
    {
        using var sp = Build(new() { [key] = value });

        Assert.Equal(NpgsqlProvider, Context(sp).Database.ProviderName);
    }

    [Fact]
    public void Postgres_environment_style_keys_override_defaults()
    {
        using var sp = Build(new()
        {
            ["POSTGRES_HOST"] = "db.internal",
            ["POSTGRES_PORT"] = "6543",
            ["POSTGRES_DB"] = "idsvc",
            ["POSTGRES_USER"] = "svc",
            ["POSTGRES_PASSWORD"] = Pw
        });

        var cs = new NpgsqlConnectionStringBuilder(Context(sp).Database.GetConnectionString());

        Assert.Equal(("db.internal", 6543, "idsvc", "svc", Pw), (cs.Host, cs.Port, cs.Database, cs.Username, cs.Password));
    }

    [Fact]
    public void Postgres_Consul_style_section_keys_are_used_when_env_keys_are_absent_and_env_wins_when_both_exist()
    {
        using var sp = Build(new()
        {
            ["Database:Postgres:Host"] = "consul-host",
            ["Database:Postgres:Database"] = "consul-db",
            ["POSTGRES_DB"] = "env-db",
            ["Database:Postgres:Password"] = Pw
        });

        var cs = new NpgsqlConnectionStringBuilder(Context(sp).Database.GetConnectionString());

        Assert.Equal("consul-host", cs.Host);
        Assert.Equal("env-db", cs.Database); // POSTGRES_DB is listed before Database:Postgres:Database
    }

    [Fact]
    public void Postgres_uses_the_identity_specific_database_key_last()
    {
        using var sp = Build(new() { ["IDENTITY_POSTGRES_DB"] = "legacy-name" });

        Assert.Equal("legacy-name", new NpgsqlConnectionStringBuilder(Context(sp).Database.GetConnectionString()).Database);
    }

    // The Npgsql options extension is an internal EF API, but it is the only place the configured migrations assembly is
    // observable offline; this test pins it on purpose.
#pragma warning disable EF1001
    [Fact]
    public void Postgres_migrations_assembly_is_the_database_project()
    {
        using var sp = Build(new());
        var extension = ((Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<IServiceProvider>)Context(sp)).Instance
            .GetRequiredService<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptions>()
            .Extensions.OfType<Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal.NpgsqlOptionsExtension>().Single();

        Assert.Equal(typeof(IdentityDbContext).Assembly.GetName().Name, extension.MigrationsAssembly);
    }
#pragma warning restore EF1001

    // ---------------------------------------------------------------- SqlServer branch
    [Theory]
    [InlineData("IdentityDb:Provider", "SqlServer")]
    [InlineData("DB_PROVIDER", "sqlserver")]
    [InlineData("DB_PROVIDER", "Sql")]
    public void SqlServer_is_selected_through_either_provider_key(string key, string value)
    {
        using var sp = Build(new() { [key] = value, ["SQLSERVER_USER"] = "sa", ["SQLSERVER_PASSWORD"] = Pw });

        Assert.Equal(SqlServerProvider, Context(sp).Database.ProviderName);
    }

    [Fact]
    public void SqlServer_environment_style_keys_build_the_connection_string()
    {
        using var sp = Build(new()
        {
            ["DB_PROVIDER"] = "SqlServer",
            ["SQLSERVER_HOST"] = "sql.internal",
            ["SQLSERVER_PORT"] = "1444",
            ["SQLSERVER_DB"] = "idsvc",
            ["SQLSERVER_USER"] = "svc",
            ["SQLSERVER_PASSWORD"] = Pw
        });

        var cs = new SqlConnectionStringBuilder(Context(sp).Database.GetConnectionString());

        Assert.Equal("sql.internal,1444", cs.DataSource);
        Assert.Equal("idsvc", cs.InitialCatalog);
        Assert.Equal("svc", cs.UserID);
        Assert.Equal(Pw, cs.Password);
        Assert.True(cs.TrustServerCertificate);
    }

    [Fact]
    public void SqlServer_historical_database_and_password_alias_keys_are_honoured()
    {
        using var sp = Build(new()
        {
            ["IdentityDb:Provider"] = "SqlServer",
            ["IDENTITY_SQLSERVER_DB"] = "legacy",
            ["MSSQL_SA_PASSWORD"] = Pw
        });

        var cs = new SqlConnectionStringBuilder(Context(sp).Database.GetConnectionString());

        Assert.Equal("legacy", cs.InitialCatalog);
        Assert.Equal("sa", cs.UserID); // Kit: password without username implies sa
        Assert.Equal(Pw, cs.Password);
    }

    [Fact]
    public void SqlServer_with_a_password_and_no_username_defaults_to_sa_host_and_database()
    {
        using var sp = Build(new() { ["DB_PROVIDER"] = "SqlServer" });

        var cs = new SqlConnectionStringBuilder(Context(sp).Database.GetConnectionString());

        Assert.Equal("localhost,1433", cs.DataSource);
        Assert.Equal("identitydb", cs.InitialCatalog);
        Assert.Equal("sa", cs.UserID);
    }

    [Fact]
    public void Missing_Postgres_password_fails_with_an_error_naming_POSTGRES_PASSWORD_and_has_no_hardcoded_default()
    {
        using var sp = BuildWithoutDefaults(new() { ["DB_PROVIDER"] = "Postgres" });

        var ex = Assert.Throws<InvalidOperationException>(() => Context(sp));

        Assert.Contains("POSTGRES_PASSWORD", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_Postgres_password_is_treated_as_missing(string? blank)
    {
        using var sp = BuildWithoutDefaults(new() { ["POSTGRES_PASSWORD"] = blank });

        var ex = Assert.Throws<InvalidOperationException>(() => Context(sp));

        Assert.Contains("POSTGRES_PASSWORD", ex.Message);
    }

    [Theory]
    [InlineData("svc", null)]
    [InlineData("svc", "")]
    [InlineData("svc", "   ")]
    public void SqlServer_username_without_a_password_fails_with_an_error_naming_SQLSERVER_PASSWORD(string user, string? blank)
    {
        using var sp = BuildWithoutDefaults(new() { ["DB_PROVIDER"] = "SqlServer", ["SQLSERVER_USER"] = user, ["SQLSERVER_PASSWORD"] = blank });

        var ex = Assert.Throws<InvalidOperationException>(() => Context(sp));

        Assert.Contains("SQLSERVER_PASSWORD", ex.Message);
    }

    [Fact]
    public void SqlServer_without_username_and_password_uses_Integrated_Security()
    {
        using var sp = BuildWithoutDefaults(new() { ["DB_PROVIDER"] = "SqlServer" });

        var cs = new SqlConnectionStringBuilder(Context(sp).Database.GetConnectionString());

        Assert.True(cs.IntegratedSecurity);
        Assert.Equal("localhost,1433", cs.DataSource);
        Assert.Equal("identitydb", cs.InitialCatalog);
        Assert.Equal(string.Empty, cs.UserID);
        Assert.Equal(string.Empty, cs.Password);
    }

    [Fact]
    public void SqlServer_command_timeout_defaults_to_30_and_follows_IdentityDb_CommandTimeoutSeconds()
    {
        using var defaults = Build(new() { ["DB_PROVIDER"] = "SqlServer" });
        using var custom = Build(new() { ["DB_PROVIDER"] = "SqlServer", ["IdentityDb:CommandTimeoutSeconds"] = "75" });
        using var invalid = Build(new() { ["DB_PROVIDER"] = "SqlServer", ["IdentityDb:CommandTimeoutSeconds"] = "-5" });

        Assert.Equal(30, Context(defaults).Database.GetCommandTimeout());
        Assert.Equal(75, Context(custom).Database.GetCommandTimeout());
        Assert.Equal(30, Context(invalid).Database.GetCommandTimeout());
    }

    // ---------------------------------------------------------------- switching
    [Fact]
    public void Provider_switching_flips_the_resolved_EF_provider_with_no_other_change()
    {
        using var postgres = Build(new() { ["DB_PROVIDER"] = "Postgres" });
        using var sqlServer = Build(new() { ["DB_PROVIDER"] = "SqlServer" });

        Assert.Equal(NpgsqlProvider, Context(postgres).Database.ProviderName);
        Assert.Equal(SqlServerProvider, Context(sqlServer).Database.ProviderName);
    }

    [Fact]
    public void IdentityDb_Provider_key_has_priority_over_DB_PROVIDER()
    {
        using var sp = Build(new() { ["IdentityDb:Provider"] = "SqlServer", ["DB_PROVIDER"] = "Postgres" });

        Assert.Equal(SqlServerProvider, Context(sp).Database.ProviderName);
    }

    [Fact]
    public void Unrecognised_provider_value_falls_back_to_Postgres_the_documented_default()
    {
        using var sp = Build(new() { ["DB_PROVIDER"] = "mysql" });

        Assert.Equal(NpgsqlProvider, Context(sp).Database.ProviderName);
    }

    [Fact]
    public void Registration_exposes_the_Port_surface_as_scoped_services()
    {
        var services = new ServiceCollection().AddIdentityDatabase(new ConfigurationBuilder().Build());

        var map = services.Where(d => d.ServiceType != typeof(IDatabaseProvider)).ToDictionary(d => d.ServiceType, d => (d.ImplementationType, d.Lifetime));
        Assert.Equal((typeof(UnitOfWork), ServiceLifetime.Scoped), map[typeof(IUnitOfWork)]);
        Assert.Equal((typeof(DbInitializer), ServiceLifetime.Scoped), map[typeof(IDbInitializer)]);
        var repository = services.Single(d => d.ServiceType == typeof(IRepository<>));
        Assert.Equal(typeof(EfRepository<>), repository.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, repository.Lifetime);
    }

    [Theory]
    [InlineData("Postgres", "AddCoreviaPostgresProvider")]
    [InlineData("SqlServer", "AddCoreviaSqlServerProvider")]
    public void Configured_engine_without_a_registered_provider_fails_naming_the_missing_registration(string engine, string extension)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DatabaseProvider"] = engine }).Build();
        using var sp = new ServiceCollection().AddCoreviaDatabase<IdentityDbContext>(configuration).BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() => Context(sp));

        Assert.Contains(extension, ex.Message);
    }
}
