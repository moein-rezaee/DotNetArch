using {{Prefix}}.Kit.{{Area}}.Abstractions;
using {{Prefix}}.Kit.{{Area}}.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace {{Prefix}}.Kit.{{Area}}.Tests;

public class ProviderSelectionTests
{
    private sealed class NamedProvider(string name) : I{{Area}}Provider
    {
        public string Name { get; } = name;

        public I{{Area}} Create(IConfiguration configuration) => throw new NotSupportedException("not needed for selection tests");
    }

    private static IConfiguration Config(string? provider) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(provider is null ? new Dictionary<string, string?>() : new() { [{{Area}}ProviderResolver.ProviderConfigKey] = provider })
            .Build();

    [Fact]
    public void A_single_registered_provider_is_used_without_configuration() =>
        Assert.Equal("One", {{Area}}ProviderResolver.Resolve(Config(null), new[] { new NamedProvider("One") }).Name);

    [Fact]
    public void Several_providers_require_an_explicit_choice()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            {{Area}}ProviderResolver.Resolve(Config(null), new[] { new NamedProvider("One"), new NamedProvider("Two") }));

        Assert.Contains({{Area}}ProviderResolver.ProviderConfigKey, error.Message);
    }

    [Fact]
    public void The_configured_provider_is_matched_case_insensitively() =>
        Assert.Equal("Two", {{Area}}ProviderResolver.Resolve(Config("two"), new[] { new NamedProvider("One"), new NamedProvider("Two") }).Name);

    [Fact]
    public void An_unknown_provider_lists_the_registered_ones()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            {{Area}}ProviderResolver.Resolve(Config("Nope"), new[] { new NamedProvider("One") }));

        Assert.Contains("One", error.Message);
    }

    [Fact]
    public void No_registered_provider_is_reported_clearly() =>
        Assert.Throws<InvalidOperationException>(() => {{Area}}ProviderResolver.Resolve(Config(null), Array.Empty<I{{Area}}Provider>()));

    [Fact]
    public void The_entry_point_registers_the_contract_as_a_singleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<I{{Area}}Provider>(new NamedProvider("One"));
        services.Add{{Area}}Kit(Config(null));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(I{{Area}}) && descriptor.Lifetime == ServiceLifetime.Singleton);
    }
}
