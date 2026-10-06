namespace {{Prefix}}.Kit.Cache.Abstractions;

/// <summary>Outcome of a cache read: distinguishes "not found" from a cached default value.</summary>
public readonly record struct CacheLookup<T>(bool Found, T? Value)
{
    public static CacheLookup<T> Miss { get; } = new(false, default);

    public static CacheLookup<T> Hit(T? value) => new(true, value);
}

/// <summary>
/// Provider-neutral key/value cache. Keys must not be empty; a null expiry means "provider default / no expiry".
/// Provider failures surface as <see cref="CacheException"/>; cancellation surfaces as <see cref="OperationCanceledException"/>.
/// </summary>
public interface ICache
{
    /// <summary>Name of the provider selected by configuration for this deployment.</summary>
    string ProviderName { get; }

    Task<CacheLookup<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

    /// <summary>Removes a key; returns whether it existed.</summary>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}
