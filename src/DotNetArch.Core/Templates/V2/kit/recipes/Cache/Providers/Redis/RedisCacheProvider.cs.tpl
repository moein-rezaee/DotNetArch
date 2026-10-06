using System.Text.Json;
using {{Prefix}}.Kit.Cache.Abstractions;
using {{Prefix}}.Kit.Cache.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace {{Prefix}}.Kit.Cache.Providers.Redis;

/// <summary>Redis provider (StackExchange.Redis). Values are stored as JSON strings; the connection is opened on first use.</summary>
public sealed class RedisCacheProvider : ICacheProvider
{
    public const string ProviderName = "Redis";

    public string Name => ProviderName;

    public ICache Create(IConfiguration configuration) => new RedisCache(RedisCacheOptions.Bind(configuration));
}

internal sealed class RedisCache : ICache, IDisposable
{
    private readonly RedisCacheOptions _options;
    private readonly Lazy<ConnectionMultiplexer> _connection;

    public RedisCache(RedisCacheOptions options)
    {
        _options = options;
        _connection = new Lazy<ConnectionMultiplexer>(() =>
        {
            var configuration = ConfigurationOptions.Parse(options.Endpoint);
            configuration.AbortOnConnectFail = false;
            if (options.Password is not null)
                configuration.Password = options.Password;
            return ConnectionMultiplexer.Connect(configuration);
        });
    }

    public string ProviderName => RedisCacheProvider.ProviderName;

    public async Task<CacheLookup<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = await Run(db => db.StringGetAsync(Key(key)), cancellationToken).ConfigureAwait(false);
        return value.IsNull
            ? CacheLookup<T>.Miss
            : CacheLookup<T>.Hit(JsonSerializer.Deserialize<T>((string)value!));
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) =>
        Run(db => db.StringSetAsync(Key(key), JsonSerializer.Serialize(value), expiry), cancellationToken);

    public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        Run(db => db.KeyDeleteAsync(Key(key)), cancellationToken);

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Run(db => db.KeyExistsAsync(Key(key)), cancellationToken);

    public void Dispose()
    {
        if (_connection.IsValueCreated)
            _connection.Value.Dispose();
    }

    private string Key(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key must not be empty.", nameof(key));
        return _options.KeyPrefix + key;
    }

    private async Task<TResult> Run<TResult>(Func<IDatabase, Task<TResult>> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await operation(_connection.Value.GetDatabase(_options.Database)).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (RedisException ex)
        {
            throw new CacheException($"Redis cache is unavailable: {ex.Message}", "cache_unavailable", ex);
        }
    }
}

public static class RedisCacheServiceCollectionExtensions
{
    /// <summary>Registers the Redis provider so <c>AddCacheKit</c> can select it.</summary>
    public static IServiceCollection AddRedisCacheProvider(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICacheProvider, RedisCacheProvider>());
        return services;
    }
}
