using {{Prefix}}.Kit.{{Area}}.Abstractions;
using {{Prefix}}.Kit.{{Area}}.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace {{Prefix}}.Kit.{{Area}}.Providers.{{Provider}};

/// <summary>
/// {{Provider}} provider. Non-sensitive options live in <c>{{Area}}:{{Provider}}:*</c>; secrets come from UPPER_CASE environment keys
/// (for example <c>{{ProviderUpper}}_SECRET</c>). TODO: implement <see cref="I{{Area}}"/> against {{Provider}}.
/// </summary>
public sealed class {{Provider}}{{Area}}Provider : I{{Area}}Provider
{
    public const string SectionPath = "{{Area}}:{{Provider}}";

    public string Name => "{{Provider}}";

    public I{{Area}} Create(IConfiguration configuration) => new {{Provider}}{{Area}}();
}

internal sealed class {{Provider}}{{Area}} : I{{Area}}
{
    public string ProviderName => "{{Provider}}";
}

public static class {{Provider}}{{Area}}ServiceCollectionExtensions
{
    /// <summary>Registers the {{Provider}} provider so <c>Add{{Area}}Kit</c> can select it.</summary>
    public static IServiceCollection Add{{Provider}}{{Area}}Provider(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<I{{Area}}Provider, {{Provider}}{{Area}}Provider>());
        return services;
    }
}
