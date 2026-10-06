using {{Prefix}}.Kit.Cache.Providers.Redis;
using Microsoft.Extensions.Configuration;

namespace {{Prefix}}.Kit.Cache.Tests;

public class RedisOptionsTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void The_endpoint_is_required_and_the_error_names_the_key()
    {
        var error = Assert.Throws<InvalidOperationException>(() => RedisCacheOptions.Bind(Config(new())));

        Assert.Contains("Cache:Redis:Endpoint", error.Message);
    }

    [Fact]
    public void Settings_and_the_secret_password_are_read_from_their_own_sources()
    {
        var options = RedisCacheOptions.Bind(Config(new()
        {
            ["Cache:Redis:Endpoint"] = "redis:6379",
            ["Cache:Redis:Database"] = "3",
            ["Cache:Redis:KeyPrefix"] = "app:",
            [RedisCacheOptions.PasswordKey] = "s3cret"
        }));

        Assert.Equal("redis:6379", options.Endpoint);
        Assert.Equal(3, options.Database);
        Assert.Equal("app:", options.KeyPrefix);
        Assert.Equal("s3cret", options.Password);
    }

    [Fact]
    public void A_bad_database_index_is_rejected() =>
        Assert.Throws<InvalidOperationException>(() => RedisCacheOptions.Bind(Config(new()
        {
            ["Cache:Redis:Endpoint"] = "redis:6379",
            ["Cache:Redis:Database"] = "-1"
        })));
}
