namespace {{App}}.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Secret key read from the environment / .env file (UPPER_CASE by convention).</summary>
    public const string ConnectionStringKey = "DATABASE_CONNECTION_STRING";

    /// <summary>Non-sensitive: which database engine the service talks to.</summary>
    public string Provider { get; set; } = "{{Provider}}";

    /// <summary>Secret: filled from <see cref="ConnectionStringKey"/>; leave empty in appsettings.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Non-sensitive: apply pending migrations during startup (handy for development).</summary>
    public bool MigrateOnStartup { get; set; }
}
