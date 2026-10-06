namespace {{App}}.Api.Configuration;

public static class AppConfigurationExtensions
{
    /// <summary>
    /// Builds <see cref="IConfiguration"/> from every source in a fixed order (later wins):
    /// appsettings.json, appsettings.{Environment}.json, .env, .env.{environment}, real environment variables, command line.
    /// Non-sensitive settings belong in appsettings; secrets and run-time values belong in the environment / .env.
    /// </summary>
    public static WebApplicationBuilder AddAppConfiguration(this WebApplicationBuilder builder, string[]? args = null)
    {
        var root = builder.Environment.ContentRootPath;
        var environmentName = builder.Environment.EnvironmentName.ToLowerInvariant();

        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddInMemoryCollection(EnvFile.Read(Path.Combine(root, ".env")))
            .AddInMemoryCollection(EnvFile.Read(Path.Combine(root, $".env.{environmentName}")))
            .AddEnvironmentVariables();

        if (args is { Length: > 0 })
            builder.Configuration.AddCommandLine(args);

        return builder;
    }
}
