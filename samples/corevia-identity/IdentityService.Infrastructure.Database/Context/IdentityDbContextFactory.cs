using Corevia.Kit.DatabaseConnection.Abstractions;
using Corevia.Kit.DatabaseConnection.Providers.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IdentityService.Infrastructure.Database.Context;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        static string? ResolveEnv(params string[] keys)
            => keys.Select(Environment.GetEnvironmentVariable)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        // Design-time only (dotnet ef): same env keys as before (host/port/database/user keep their defaults; the password has no default), but the connection string and
        // the UseNpgsql wiring come from Corevia.Kit.DatabaseConnection instead of being built inline.
        var settings = new DatabaseConnectionSettings
        {
            Host = ResolveEnv("POSTGRES_HOST") ?? "localhost",
            Port = ResolveEnv("POSTGRES_PORT") ?? "5432",
            Database = ResolveEnv("IDENTITY_POSTGRES_DB", "POSTGRES_DB") ?? "identitydb",
            Username = ResolveEnv("POSTGRES_USER") ?? "postgres",
            Password = ResolveEnv("POSTGRES_PASSWORD")
                ?? throw new InvalidOperationException(
                    "POSTGRES_PASSWORD must be set to run design-time EF tooling (no hardcoded fallback).")
        };

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseCoreviaPostgres(settings, typeof(IdentityDbContext).Assembly.GetName().Name!);

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
