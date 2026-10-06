using Corevia.Kit.DatabaseConnection.Core;
using Corevia.Kit.DatabaseConnection.Providers.Postgres;
using Corevia.Kit.DatabaseConnection.Providers.SqlServer;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Database.Context;
using IdentityService.Infrastructure.Database.Initialization;
using IdentityService.Infrastructure.Database.Providers;
using IdentityService.Infrastructure.Database.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure.Database;

/// <summary>
/// Single registration entry point for IdentityService's own-storage infrastructure
/// (DbContext, repositories, UnitOfWork, DbInitializer). Delegates provider selection
/// and EF Core wiring to <c>Corevia.Kit.DatabaseConnection</c>'s
/// <c>AddCoreviaDatabase&lt;TContext&gt;</c> so IdentityService never branches on engine
/// itself. The configuration overrides below preserve IdentityService's historical
/// config-key names and defaults exactly as implemented by the now-removed
/// IdentityService.Infrastructure.Postgres/.Infrastructure.SqlServer projects.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddIdentityDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Composition root: register every engine this service supports. Which one is used is still
        // chosen at runtime by configuration (IdentityDb:Provider / DB_PROVIDER); no engine branching here.
        services.AddCoreviaPostgresProvider()
            .AddCoreviaSqlServerProvider();

        // The password is a secret and has no default: wrap the Kit providers (the last registration of a
        // kind wins in AddCoreviaDatabase) so a missing password fails with an error naming the secret key.
        // SQL Server only requires it when a username is supplied; with neither, the Kit builds an
        // Integrated Security connection string as before.
        services.AddSingleton<IDatabaseProvider>(new RequiredPasswordDatabaseProvider(new PostgresDatabaseProvider(), "POSTGRES_PASSWORD"));
        services.AddSingleton<IDatabaseProvider>(new RequiredPasswordDatabaseProvider(new SqlServerDatabaseProvider(), "SQLSERVER_PASSWORD", s => !string.IsNullOrWhiteSpace(s.Username)));

        services.AddCoreviaDatabase<IdentityDbContext>(configuration, options =>
        {
            // Preserve the historical provider-selection keys ("IdentityDb:Provider" from
            // Consul-backed config, "DB_PROVIDER" from environment). Default provider stays
            // Postgres, matching Corevia.Kit.DatabaseConnection's own default.
            options.ProviderConfigKeys = new[] { "IdentityDb:Provider", "DB_PROVIDER" };

            options.Postgres.HostKeys = new[] { "POSTGRES_HOST", "Database:Postgres:Host" };
            options.Postgres.HostDefault = "localhost";
            options.Postgres.PortKeys = new[] { "POSTGRES_PORT", "Database:Postgres:Port" };
            options.Postgres.PortDefault = "5432";
            options.Postgres.DatabaseKeys = new[] { "POSTGRES_DB", "Database:Postgres:Database", "IDENTITY_POSTGRES_DB" };
            options.Postgres.DatabaseDefault = "identitydb";
            options.Postgres.UsernameKeys = new[] { "POSTGRES_USER", "Database:Postgres:Username" };
            options.Postgres.UsernameDefault = "postgres";
            options.Postgres.PasswordKeys = new[] { "POSTGRES_PASSWORD", "Database:Postgres:Password" };

            options.SqlServer.HostKeys = new[] { "SQLSERVER_HOST", "Database:SqlServer:Host" };
            options.SqlServer.HostDefault = "localhost";
            options.SqlServer.PortKeys = new[] { "SQLSERVER_PORT", "Database:SqlServer:Port" };
            options.SqlServer.PortDefault = "1433";
            options.SqlServer.DatabaseKeys = new[] { "SQLSERVER_DB", "IDENTITY_SQLSERVER_DB", "Database:SqlServer:Database" };
            options.SqlServer.DatabaseDefault = "identitydb";
            options.SqlServer.UsernameKeys = new[] { "SQLSERVER_USER", "Database:SqlServer:Username" };
            options.SqlServer.PasswordKeys = new[] { "SQLSERVER_PASSWORD", "MSSQL_SA_PASSWORD", "Database:SqlServer:Password" };

            options.CommandTimeoutSeconds = ResolveCommandTimeout(configuration);
        });

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDbInitializer, DbInitializer>();

        return services;
    }

    private static int ResolveCommandTimeout(IConfiguration configuration)
    {
        return int.TryParse(configuration["IdentityDb:CommandTimeoutSeconds"], out var timeout) && timeout > 0
            ? timeout
            : 30;
    }
}
