using Corevia.Kit.MediaStorage.Abstractions;
using Corevia.Kit.MediaStorage.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Corevia.Kit.MediaStorage.Providers.Minio;

/// <summary>
/// Minio provider. Non-sensitive options: <c>MediaStorage:Minio:Endpoint|Bucket|Region|ForcePathStyle|PublicBaseUrl</c>.
/// Secrets (environment/Vault, UPPER_CASE): <c>MINIO_ACCESS_KEY</c> and <c>MINIO_SECRET_KEY</c>; leave both
/// empty for anonymous access to a public bucket.
/// </summary>
public sealed class MinioMediaStorageProvider : IMediaStorageProvider
{
    public const string SectionPath = "MediaStorage:Minio";
    public const string AccessKeyName = "MINIO_ACCESS_KEY";
    public const string SecretKeyName = "MINIO_SECRET_KEY";

    private readonly HttpMessageHandler? _handler;

    /// <param name="handler">Optional HTTP handler override (tests/stubs); null uses the SDK transport.</param>
    public MinioMediaStorageProvider(HttpMessageHandler? handler = null) => _handler = handler;

    public MediaStorageProviderKind Kind => MediaStorageProviderKind.Minio;

    public IMediaStorage Create(IConfiguration configuration)
    {
        var options = S3StorageOptionsBinder.Bind(configuration, SectionPath, AccessKeyName, SecretKeyName);
        Console.WriteLine(
            $"[Corevia.Kit.MediaStorage] Minio endpoint '{options.Endpoint}', bucket '{options.Bucket}', " +
            $"{(options.IsAnonymous ? "anonymous" : "credentialed")} access.");
        return new S3CompatibleMediaStorage(Kind, options, S3ClientFactory.Create(options, _handler));
    }
}

public static class MinioMediaStorageServiceCollectionExtensions
{
    /// <summary>Registers the Minio provider so <c>AddCoreviaMediaStorage</c> can select it.</summary>
    public static IServiceCollection AddCoreviaMinioProvider(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMediaStorageProvider, MinioMediaStorageProvider>());
        return services;
    }
}
