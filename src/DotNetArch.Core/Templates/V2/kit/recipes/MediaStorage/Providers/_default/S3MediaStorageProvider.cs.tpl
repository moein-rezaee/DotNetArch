using {{Prefix}}.Kit.MediaStorage.Abstractions;
using {{Prefix}}.Kit.MediaStorage.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace {{Prefix}}.Kit.MediaStorage.Providers.{{Provider}};

/// <summary>
/// {{Provider}} provider (S3-compatible). Non-sensitive options: <c>MediaStorage:{{Provider}}:Endpoint|Bucket|Region|ForcePathStyle|PublicBaseUrl</c>.
/// Secrets (environment / secret store, UPPER_CASE): <c>{{ProviderUpper}}_ACCESS_KEY</c> and <c>{{ProviderUpper}}_SECRET_KEY</c>;
/// leave both empty for anonymous access to a public bucket.
/// </summary>
public sealed class {{Provider}}MediaStorageProvider : IMediaStorageProvider
{
    public const string SectionPath = "MediaStorage:{{Provider}}";
    public const string AccessKeyName = "{{ProviderUpper}}_ACCESS_KEY";
    public const string SecretKeyName = "{{ProviderUpper}}_SECRET_KEY";

    private readonly HttpMessageHandler? _handler;

    /// <param name="handler">Optional HTTP handler override (tests/stubs); null uses the SDK transport.</param>
    public {{Provider}}MediaStorageProvider(HttpMessageHandler? handler = null) => _handler = handler;

    public string Name => "{{Provider}}";

    public IMediaStorage Create(IConfiguration configuration)
    {
        var options = S3StorageOptionsBinder.Bind(configuration, SectionPath, AccessKeyName, SecretKeyName);
        return new S3CompatibleMediaStorage(Name, options, S3ClientFactory.Create(options, _handler));
    }
}

public static class {{Provider}}MediaStorageServiceCollectionExtensions
{
    /// <summary>Registers the {{Provider}} provider so <c>AddMediaStorageKit</c> can select it.</summary>
    public static IServiceCollection Add{{Provider}}MediaStorageProvider(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMediaStorageProvider, {{Provider}}MediaStorageProvider>());
        return services;
    }
}
