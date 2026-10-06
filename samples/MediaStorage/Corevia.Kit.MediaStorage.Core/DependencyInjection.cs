using Corevia.Kit.MediaStorage.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Corevia.Kit.MediaStorage.Core;

public static class MediaStorageServiceCollectionExtensions
{
    /// <summary>
    /// The single entry point. Reads <c>MediaStorage:Provider</c> (default <c>Minio</c>) at registration
    /// time, so an unknown value fails at startup, and registers <see cref="IMediaStorage"/> as a
    /// singleton backed by the registered provider of that kind. Register the supported providers in the
    /// composition root with <c>AddCoreviaMinioProvider()</c> / <c>AddCoreviaRustFsProvider()</c>.
    /// </summary>
    public static IServiceCollection AddCoreviaMediaStorage(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var kind = MediaStorageProviderResolver.Resolve(configuration);

        // Console (not ILogger): registration runs before the logging pipeline exists. Never prints secrets.
        Console.WriteLine($"[Corevia.Kit.MediaStorage] Media storage provider resolved to '{kind}'.");

        services.AddSingleton<IMediaStorage>(serviceProvider =>
        {
            var provider = serviceProvider
                .GetServices<IMediaStorageProvider>()
                .LastOrDefault(candidate => candidate.Kind == kind)
                ?? throw new InvalidOperationException(
                    $"Media storage provider '{kind}' is configured but no matching provider is registered. " +
                    $"Register it in the composition root with AddCoreviaXxxProvider() (package Corevia.Kit.MediaStorage.Providers.{kind}).");
            return provider.Create(configuration);
        });
        return services;
    }
}
