using Microsoft.Extensions.Configuration;

namespace {{Prefix}}.Kit.{{Area}}.Core;

/// <summary>Chooses the registered provider from <c>{{Area}}:Provider</c>. There is no silent default when several providers are registered.</summary>
public static class {{Area}}ProviderResolver
{
    public const string ProviderConfigKey = "{{Area}}:Provider";

    public static I{{Area}}Provider Resolve(IConfiguration configuration, IEnumerable<I{{Area}}Provider> providers)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(providers);

        var registered = providers.ToList();
        if (registered.Count == 0)
        {
            throw new InvalidOperationException(
                "No {{Area}} provider is registered. Register at least one in the composition root with Add<Provider>{{Area}}Provider().");
        }

        var configured = configuration[ProviderConfigKey]?.Trim();
        if (string.IsNullOrEmpty(configured))
        {
            if (registered.Count == 1)
                return registered[0];

            throw new InvalidOperationException(
                $"'{ProviderConfigKey}' is required because several providers are registered ({string.Join(", ", registered.Select(p => p.Name))}).");
        }

        // The last registration of a name wins, so applications can override a provider.
        var match = registered.LastOrDefault(provider => string.Equals(provider.Name, configured, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new InvalidOperationException(
            $"Unknown {{Area}} provider '{configured}' in '{ProviderConfigKey}'. Registered providers: {string.Join(", ", registered.Select(p => p.Name))}.");
    }
}
