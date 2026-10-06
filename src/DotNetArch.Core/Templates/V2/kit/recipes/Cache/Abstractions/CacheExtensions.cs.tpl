namespace {{Prefix}}.Kit.Cache.Abstractions;

public static class CacheExtensions
{
    /// <summary>Returns the cached value or creates, stores and returns it. Concurrent callers may each run the factory (no locking).</summary>
    public static async Task<T> GetOrCreateAsync<T>(
        this ICache cache,
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(factory);

        var lookup = await cache.GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        if (lookup.Found)
            return lookup.Value!;

        var created = await factory(cancellationToken).ConfigureAwait(false);
        await cache.SetAsync(key, created, expiry, cancellationToken).ConfigureAwait(false);
        return created;
    }
}
