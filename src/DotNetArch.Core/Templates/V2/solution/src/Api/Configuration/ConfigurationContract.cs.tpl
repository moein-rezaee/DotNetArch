using {{App}}.Infrastructure.Persistence;

namespace {{App}}.Api.Configuration;

/// <summary>
/// The configuration surface of this service. Tests keep <c>.env.example</c> and <c>appsettings.example.json</c> in sync with it:
/// every secret / run-time key listed here must appear in <c>.env.example</c>, and the example settings must mirror <c>appsettings.json</c>.
/// </summary>
public static class ConfigurationContract
{
    /// <summary>Secret and run-time keys read from the environment or <c>.env</c> (UPPER_CASE, single underscores).</summary>
    public static IReadOnlyList<string> SecretKeys { get; } = new[]
    {
        DatabaseOptions.ConnectionStringKey,
        // <dotnet-arch:secret-keys> (new kits add their keys above this line)
    };
}
