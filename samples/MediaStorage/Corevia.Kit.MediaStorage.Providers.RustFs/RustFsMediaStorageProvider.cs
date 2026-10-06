using Corevia.Kit.MediaStorage.Abstractions;
using Corevia.Kit.MediaStorage.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Corevia.Kit.MediaStorage.Providers.RustFs;

/// <summary>
/// RustFs provider. Non-sensitive options: <c>MediaStorage:RustFs:Endpoint|Bucket|Region|ForcePathStyle|PublicBaseUrl</c>.
/// Secrets (environment/Vault, UPPER_CASE): <c>RUSTFS_ACCESS_KEY</c> and <c>RUSTFS_SECRET_KEY</c>; leave both
/// empty for anonymous access to a public bucket.
/// </summary>
public sealed class RustFsMediaStorageProvider : IMediaStorageProvider
{
    public const string SectionPath = "MediaStorage:RustFs";
    public const string AccessKeyName = "RUSTFS_ACCESS_KEY";
    public const string SecretKeyName = "RUSTFS_SECRET_KEY";

    private readonly HttpMessageHandler? _handler;

    /// <param name="handler">Optional HTTP handler override (tests/stubs); null uses the SDK transport.</param>
    public RustFsMediaStorageProvider(HttpMessageHandler? handler = null) => _handler = handler;

    public MediaStorageProviderKind Kind => MediaStorageProviderKind.RustFs;

    public IMediaStorage Create(IConfiguration configuration)
    {
        var options = S3StorageOptionsBinder.Bind(configuration, SectionPath, AccessKeyName, SecretKeyName);
        Console.WriteLine(
            $"[Corevia.Kit.MediaStorage] RustFs endpoint '{options.Endpoint}', bucket '{options.Bucket}', " +
            $"{(options.IsAnonymous ? "anonymous" : "credentialed")} access.");
        return new S3CompatibleMediaStorage(Kind, options, S3ClientFactory.Create(options, _handler));
    }
}

public static class RustFsMediaStorageServiceCollectionExtensions
{
    /// <summary>Registers the RustFs provider so <c>AddCoreviaMediaStorage</c> can select it.</summary>
    public static IServiceCollection AddCoreviaRustFsProvider(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMediaStorageProvider, RustFsMediaStorageProvider>());
        return services;
    }
}
