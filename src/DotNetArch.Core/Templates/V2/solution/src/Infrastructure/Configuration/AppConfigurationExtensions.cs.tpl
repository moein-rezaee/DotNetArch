using Microsoft.Extensions.Configuration;

namespace {{App}}.Infrastructure.Configuration;

public static class AppConfigurationExtensions
{
    /// <summary>
    /// The single configuration-load step shared by every host (Api, Mcp). Sources, later wins:
    /// appsettings.json, appsettings.{Environment}.json, .env, .env.{environment}, real environment variables, command line.
    /// Non-sensitive settings belong in appsettings; secrets and run-time values belong in the environment / .env.
    /// Call it before any options binding.
    /// </summary>
    public static IConfigurationBuilder AddAppConfiguration(
        this IConfigurationBuilder configuration,
        string contentRootPath,
        string environmentName,
        string[]? args = null)
    {
        var environment = environmentName.ToLowerInvariant();

        configuration
            .AddJsonFile(Path.Combine(contentRootPath, "appsettings.json"), optional: false, reloadOnChange: true)
            .AddJsonFile(Path.Combine(contentRootPath, $"appsettings.{environmentName}.json"), optional: true, reloadOnChange: true)
            .AddInMemoryCollection(EnvFile.Read(Path.Combine(contentRootPath, ".env")))
            .AddInMemoryCollection(EnvFile.Read(Path.Combine(contentRootPath, $".env.{environment}")))
            .AddEnvironmentVariables();

        if (args is { Length: > 0 })
            configuration.AddCommandLine(args);

        return configuration;
    }
}
