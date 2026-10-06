using Microsoft.Extensions.Configuration;

namespace {{Prefix}}.Kit.Cache.Providers.Redis;

/// <summary>
/// Validated Redis settings. Non-sensitive keys come from <c>Cache:Redis:*</c>; the password is a secret read from
/// <c>REDIS_PASSWORD</c> (environment / secret store). No endpoint default and no silent fallback.
/// </summary>
internal sealed class RedisCacheOptions
{
    public const string SectionPath = "Cache:Redis";
    public const string PasswordKey = "REDIS_PASSWORD";

    public string Endpoint { get; init; } = string.Empty;

    public int Database { get; init; }

    public string KeyPrefix { get; init; } = string.Empty;

    public string? Password { get; init; }

    public static RedisCacheOptions Bind(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SectionPath);

        var endpoint = section["Endpoint"]?.Trim();
        if (string.IsNullOrEmpty(endpoint))
            throw new InvalidOperationException($"Cache option '{SectionPath}:Endpoint' is required but is not configured.");

        var database = 0;
        var rawDatabase = section["Database"]?.Trim();
        if (!string.IsNullOrEmpty(rawDatabase) && (!int.TryParse(rawDatabase, out database) || database < 0))
            throw new InvalidOperationException($"Cache option '{SectionPath}:Database' must be a non-negative integer but was '{rawDatabase}'.");

        var password = configuration[PasswordKey];
        return new RedisCacheOptions
        {
            Endpoint = endpoint,
            Database = database,
            KeyPrefix = section["KeyPrefix"]?.Trim() ?? string.Empty,
            Password = string.IsNullOrWhiteSpace(password) ? null : password
        };
    }
}
