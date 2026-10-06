namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>Database engines supported by the v2 template and everything that differs between them.</summary>
public static class DatabaseProviders
{
    public const string Sqlite = "SQLite";
    public const string SqlServer = "SqlServer";
    public const string Postgres = "Postgres";

    public static readonly string[] Supported = { Sqlite, SqlServer, Postgres };

    public static bool IsSupported(string? provider) =>
        Supported.Contains(provider ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string provider) =>
        Supported.First(s => s.Equals(provider, StringComparison.OrdinalIgnoreCase));

    public static string PackageId(string provider) => provider switch
    {
        Sqlite => "Microsoft.EntityFrameworkCore.Sqlite",
        SqlServer => "Microsoft.EntityFrameworkCore.SqlServer",
        Postgres => "Npgsql.EntityFrameworkCore.PostgreSQL",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported database provider.")
    };

    public static string UseStatement(string provider) => provider switch
    {
        Sqlite => "options.UseSqlite(database.ConnectionString);",
        SqlServer => "options.UseSqlServer(database.ConnectionString);",
        Postgres => "options.UseNpgsql(database.ConnectionString);",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported database provider.")
    };

    /// <summary>Connection string placeholder for examples and design-time tooling; never contains a real secret.</summary>
    public static string ExampleConnectionString(string provider, string databaseName) => provider switch
    {
        Sqlite => "Data Source=app.db",
        SqlServer => $"Server=localhost,1433;Database={databaseName};User Id=sa;Password=<your-password>;TrustServerCertificate=True",
        Postgres => $"Host=localhost;Database={databaseName};Username=postgres;Password=<your-password>",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported database provider.")
    };
}
