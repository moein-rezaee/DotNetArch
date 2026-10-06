using {{Prefix}}.Kit.{{Area}}.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace {{Prefix}}.Kit.{{Area}}.Core;

public static class {{Area}}ServiceCollectionExtensions
{
    /// <summary>
    /// The single entry point. Registers <see cref="I{{Area}}"/> as a singleton backed by the provider selected through
    /// <c>{{Area}}:Provider</c>. Register the providers first (each package offers its own <c>Add&lt;Provider&gt;{{Area}}Provider()</c>).
    /// An unknown or missing selection fails at first resolve with a message naming the key.
    /// </summary>
    public static IServiceCollection Add{{Area}}Kit(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<I{{Area}}>(serviceProvider =>
            {{Area}}ProviderResolver.Resolve(configuration, serviceProvider.GetServices<I{{Area}}Provider>()).Create(configuration));
        return services;
    }
}
