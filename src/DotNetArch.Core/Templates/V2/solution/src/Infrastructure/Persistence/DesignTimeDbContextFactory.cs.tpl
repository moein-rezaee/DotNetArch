using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace {{App}}.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> (migrations). Keeps EF tooling out of the Api project.
/// Reads <c>DATABASE_CONNECTION_STRING</c> and falls back to a local development database.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var database = new DatabaseOptions
        {
            ConnectionString = Environment.GetEnvironmentVariable(DatabaseOptions.ConnectionStringKey)
                ?? "{{DesignTimeConnectionString}}"
        };

        var options = new DbContextOptionsBuilder<AppDbContext>();
        {{UseProviderStatement}}
        return new AppDbContext(options.Options);
    }
}
