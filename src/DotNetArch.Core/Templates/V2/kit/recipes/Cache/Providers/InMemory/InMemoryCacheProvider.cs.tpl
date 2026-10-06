using {{Prefix}}.Kit.Cache.Abstractions;
using {{Prefix}}.Kit.Cache.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace {{Prefix}}.Kit.Cache.Providers.InMemory;

/// <summary>In-process cache: single node only, ideal for development and tests. Needs no configuration or secrets.</summary>
public sealed class InMemoryCacheProvider : ICacheProvider
{
    public const string ProviderName = "InMemory";

    public string Name => ProviderName;

    public ICache Create(IConfiguration configuration) => new InMemoryCache();
}

internal sealed class InMemoryCache : ICache, IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public string ProviderName => InMemoryCacheProvider.ProviderName;

    public Task<CacheLookup<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        RequireKey(key);
        var lookup = _cache.TryGetValue(key, out var value) && value is T typed
            ? CacheLookup<T>.Hit(typed)
            : CacheLookup<T>.Miss;
        return Task.FromResult(lookup);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        RequireKey(key);
        using var entry = _cache.CreateEntry(key);
        entry.Value = value;
        if (expiry is { } ttl)
            entry.AbsoluteExpirationRelativeToNow = ttl;
        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        RequireKey(key);
        var existed = _cache.TryGetValue(key, out _);
        _cache.Remove(key);
        return Task.FromResult(existed);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        RequireKey(key);
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }

    public void Dispose() => _cache.Dispose();

    private static void RequireKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key must not be empty.", nameof(key));
    }
}

public static class InMemoryCacheServiceCollectionExtensions
{
    /// <summary>Registers the InMemory provider so <c>AddCacheKit</c> can select it.</summary>
    public static IServiceCollection AddInMemoryCacheProvider(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICacheProvider, InMemoryCacheProvider>());
        return services;
    }
}
