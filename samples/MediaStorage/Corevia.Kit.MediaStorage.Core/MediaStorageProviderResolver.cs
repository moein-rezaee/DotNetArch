using Corevia.Kit.MediaStorage.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Corevia.Kit.MediaStorage.Core;

/// <summary>Resolves <c>MediaStorage:Provider</c> (default <c>Minio</c>; case-insensitive).</summary>
public static class MediaStorageProviderResolver
{
    public const string ProviderConfigKey = "MediaStorage:Provider";

    public static MediaStorageProviderKind Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var raw = configuration[ProviderConfigKey];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return MediaStorageProviderKind.Minio;
        }

        var normalized = raw.Trim();
        foreach (var kind in Enum.GetValues<MediaStorageProviderKind>())
        {
            if (string.Equals(kind.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        throw new InvalidOperationException(
            $"Unknown media storage provider '{normalized}' in '{ProviderConfigKey}'. " +
            $"Supported values: {string.Join(", ", Enum.GetNames<MediaStorageProviderKind>())}.");
    }
}
