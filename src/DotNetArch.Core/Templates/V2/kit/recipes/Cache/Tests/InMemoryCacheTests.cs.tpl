using {{Prefix}}.Kit.Cache.Abstractions;
using {{Prefix}}.Kit.Cache.Providers.InMemory;
using Microsoft.Extensions.Configuration;

namespace {{Prefix}}.Kit.Cache.Tests;

public sealed class InMemoryCacheTests : IDisposable
{
    private readonly ICache _cache = new InMemoryCacheProvider().Create(new ConfigurationBuilder().Build());

    public void Dispose() => (_cache as IDisposable)?.Dispose();

    [Fact]
    public async Task Stores_and_returns_values()
    {
        await _cache.SetAsync("answer", 42);

        var lookup = await _cache.GetAsync<int>("answer");

        Assert.True(lookup.Found);
        Assert.Equal(42, lookup.Value);
        Assert.True(await _cache.ExistsAsync("answer"));
    }

    [Fact]
    public async Task A_cached_default_value_is_still_a_hit()
    {
        await _cache.SetAsync("zero", 0);

        Assert.True((await _cache.GetAsync<int>("zero")).Found);
        Assert.False((await _cache.GetAsync<int>("missing")).Found);
    }

    [Fact]
    public async Task Remove_reports_whether_the_key_existed()
    {
        await _cache.SetAsync("k", "v");

        Assert.True(await _cache.RemoveAsync("k"));
        Assert.False(await _cache.RemoveAsync("k"));
    }

    [Fact]
    public async Task Entries_expire()
    {
        await _cache.SetAsync("short", "v", TimeSpan.FromMilliseconds(50));
        await Task.Delay(300);

        Assert.False((await _cache.GetAsync<string>("short")).Found);
    }

    [Fact]
    public async Task GetOrCreate_runs_the_factory_once_per_key()
    {
        var calls = 0;
        Task<string> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult("created");
        }

        Assert.Equal("created", await _cache.GetOrCreateAsync("k", Factory));
        Assert.Equal("created", await _cache.GetOrCreateAsync("k", Factory));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Empty_keys_are_rejected() =>
        await Assert.ThrowsAsync<ArgumentException>(() => _cache.SetAsync(" ", 1));
}
