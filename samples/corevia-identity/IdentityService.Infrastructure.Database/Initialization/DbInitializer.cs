using System.Data.Common;
using System.Net;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Database.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace IdentityService.Infrastructure.Database.Initialization;

public sealed class DbInitializer(
    IdentityDbContext dbContext,
    IIdentitySeedService seedService,
    ILogger<DbInitializer> logger) : IDbInitializer
{
    private readonly IdentityDbContext _dbContext = dbContext;
    private readonly IIdentitySeedService _seedService = seedService;
    private readonly ILogger<DbInitializer> _logger = logger;

    private const string ResetEnvVar = "IDENTITY_DB_RESET";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔷 Initializing identity database...");

            if (IsSqlServerProvider())
            {
                await InitializeSqlServerAsync(cancellationToken);
                return;
            }

            var connectionString = _dbContext.Database.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("❌ Database connection string is empty or null.");
                throw new InvalidOperationException("Database connection string is not configured.");
            }

            var connBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            var databaseName = connBuilder.Database;
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new InvalidOperationException("Database name is missing from the Postgres connection string.");
            }

            bool canConnect;
            Exception? connectException = null;
            try
            {
                canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                canConnect = false;
                connectException = ex;
            }
            if (!canConnect)
            {
                if (connectException is not null)
                {
                    _logger.LogWarning(connectException, "⚠️ Cannot connect to database '{DatabaseName}'. Attempting to create it...", databaseName);
                }
                else
                {
                    _logger.LogWarning("⚠️ Cannot connect to database '{DatabaseName}'. Attempting to create it...", databaseName);
                }

                connBuilder.Database = "postgres";

                await using var masterConnection = new NpgsqlConnection(connBuilder.ToString());
                await masterConnection.OpenAsync(cancellationToken);

                await using var checkCommand = new NpgsqlCommand(
                    "SELECT 1 FROM pg_database WHERE datname = @dbName",
                    masterConnection);
                checkCommand.Parameters.AddWithValue("dbName", databaseName);

                var exists = await checkCommand.ExecuteScalarAsync(cancellationToken);

                if (exists == null)
                {
                    _logger.LogInformation("📝 Creating database '{DatabaseName}'...", databaseName);

                    await using var createCommand = new NpgsqlCommand(
                        $"CREATE DATABASE \"{databaseName}\"",
                        masterConnection);
                    await createCommand.ExecuteNonQueryAsync(cancellationToken);

                    _logger.LogInformation("✅ Database '{DatabaseName}' created successfully.", databaseName);
                }
                else
                {
                    _logger.LogInformation("ℹ️ Database '{DatabaseName}' already exists.", databaseName);
                }
            }

            await EnsureMigrationsCompatibleAsync(connectionString, cancellationToken);

            // Older production databases may have been created from the initial schema
            // before customer refresh tokens became non-expiring. Keep startup compatible
            // while the EF migration history catches up.
            await EnsureRefreshTokenExpiryIsNullableAsync(cancellationToken);

            _logger.LogInformation("📦 Applying database migrations...");
            await _dbContext.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("✅ Database migrations applied successfully.");

            _logger.LogInformation("🌱 Starting seed data operation...");
            await _seedService.SeedAsync(cancellationToken);
            _logger.LogInformation("✅ Identity seed completed successfully.");
        }
        catch (DbException ex)
        {
            // The own-storage dependency is unreachable or rejected us: surface it as the Kit's typed external-dependency
            // failure (service unavailable) instead of a provider-specific exception.
            _logger.LogError(ex, "❌ Error initializing identity database. Details: {Message}", ex.Message);
            throw new ExternalServiceException(
                "Identity database is unavailable.",
                HttpStatusCode.ServiceUnavailable,
                "database_unavailable",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error initializing identity database. Details: {Message}", ex.Message);
            throw;
        }
    }

    private async Task InitializeSqlServerAsync(CancellationToken cancellationToken)
    {
        var connectionString = _dbContext.Database.GetConnectionString();
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogError("❌ SQL Server connection string is empty or null.");
            throw new InvalidOperationException("SQL Server connection string is not configured.");
        }

        var connBuilder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = connBuilder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("Database name is missing from the SQL Server connection string.");
        }

        await EnsureSqlServerDatabaseExistsAsync(connBuilder, databaseName, cancellationToken);

        _logger.LogInformation("📦 Ensuring SQL Server identity database schema exists...");
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureSqlServerRefreshTokenRotationSchemaAsync(cancellationToken);
        _logger.LogInformation("✅ SQL Server identity database schema is ready.");

        _logger.LogInformation("🌱 Starting seed data operation...");
        await _seedService.SeedAsync(cancellationToken);
        _logger.LogInformation("✅ Identity seed completed successfully.");
    }

    private bool IsSqlServerProvider()
    {
        return _dbContext.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task EnsureSqlServerRefreshTokenRotationSchemaAsync(CancellationToken cancellationToken)
    {
        // SQL Server customer databases are initialized with EnsureCreatedAsync.
        // EnsureCreated does not evolve an already-existing schema, so additive
        // model changes must be applied idempotently before EF issues queries
        // against the updated RefreshToken model.
        const string sql = """
                           IF OBJECT_ID(N'[dbo].[RefreshTokens]', N'U') IS NOT NULL
                              AND COL_LENGTH(N'dbo.RefreshTokens', N'ReplacedByTokenId') IS NULL
                           BEGIN
                               ALTER TABLE [dbo].[RefreshTokens]
                               ADD [ReplacedByTokenId] uniqueidentifier NULL;
                           END;
                           """;

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureSqlServerDatabaseExistsAsync(
        SqlConnectionStringBuilder connBuilder,
        string databaseName,
        CancellationToken cancellationToken)
    {
        connBuilder.InitialCatalog = "master";

        await using var masterConnection = new SqlConnection(connBuilder.ToString());
        await masterConnection.OpenAsync(cancellationToken);

        await using var checkCommand = new SqlCommand(
            "SELECT COUNT(1) FROM sys.databases WHERE name = @dbName;",
            masterConnection);
        checkCommand.Parameters.AddWithValue("@dbName", databaseName);

        var exists = (int)(await checkCommand.ExecuteScalarAsync(cancellationToken) ?? 0);
        if (exists > 0)
        {
            _logger.LogInformation("ℹ️ SQL Server database '{DatabaseName}' already exists.", databaseName);
            return;
        }

        _logger.LogInformation("📝 Creating SQL Server database '{DatabaseName}'...", databaseName);
        var safeDatabaseName = databaseName.Replace("]", "]]", StringComparison.Ordinal);
        await using var createCommand = new SqlCommand(
            $"CREATE DATABASE [{safeDatabaseName}]",
            masterConnection);
        await createCommand.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("✅ SQL Server database '{DatabaseName}' created successfully.", databaseName);
    }

    private async Task EnsureRefreshTokenExpiryIsNullableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           DO $$
                           BEGIN
                               IF EXISTS (
                                   SELECT 1
                                   FROM information_schema.columns
                                   WHERE table_schema = 'public'
                                     AND table_name = 'RefreshTokens'
                                     AND column_name = 'ExpiresAt'
                                     AND is_nullable = 'NO'
                               ) THEN
                                   ALTER TABLE "RefreshTokens" ALTER COLUMN "ExpiresAt" DROP NOT NULL;
                               END IF;
                           END $$;
                           """;

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureMigrationsCompatibleAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        var hasHistory = await TableExistsAsync(conn, "__EFMigrationsHistory", cancellationToken);
        var hasAnyTables = await HasAnyUserTablesAsync(conn, cancellationToken);

        if (hasHistory || !hasAnyTables)
        {
            return;
        }

        var canReset = string.Equals(
            Environment.GetEnvironmentVariable(ResetEnvVar),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (!canReset)
        {
            throw new InvalidOperationException(
                $"Database exists but has no EF migrations history table (__EFMigrationsHistory). " +
                $"This usually means the schema was created via EnsureCreated() and is now out of sync with migrations. " +
                $"To reset (DATA LOSS), set {ResetEnvVar}=true for one run and restart the service.");
        }

        _logger.LogWarning(
            "⚠️ Legacy schema detected (no EF migrations history). Resetting public schema because {EnvVar}=true (DATA LOSS).",
            ResetEnvVar);
        await ResetPublicSchemaAsync(conn, cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection conn, string tableName, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT EXISTS (
                               SELECT 1
                               FROM pg_class c
                               JOIN pg_namespace n ON n.oid = c.relnamespace
                               WHERE n.nspname = 'public'
                                 AND c.relkind = 'r'
                                 AND lower(c.relname) = lower(@table)
                           );
                           """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("table", tableName);
        return (bool)(await cmd.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<bool> HasAnyUserTablesAsync(NpgsqlConnection conn, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT EXISTS (
                               SELECT 1
                               FROM pg_class c
                               JOIN pg_namespace n ON n.oid = c.relnamespace
                               WHERE n.nspname = 'public'
                                 AND c.relkind = 'r'
                                 AND lower(c.relname) <> lower('__EFMigrationsHistory')
                           );
                           """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        return (bool)(await cmd.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task ResetPublicSchemaAsync(NpgsqlConnection conn, CancellationToken cancellationToken)
    {
        const string sql = """
                           DROP SCHEMA IF EXISTS public CASCADE;
                           CREATE SCHEMA public;
                           """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
