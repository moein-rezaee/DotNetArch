using Microsoft.Extensions.Configuration;

namespace Corevia.Kit.MediaStorage.Core;

/// <summary>
/// Reads and validates one provider's options. Non-sensitive keys come from the provider section
/// (for example <c>MediaStorage:Minio:Endpoint</c>); the two secrets come from UPPER_CASE keys
/// (environment/Vault, for example <c>MINIO_ACCESS_KEY</c>). There are no hardcoded endpoint or
/// bucket defaults and no silent fallback: a missing required key or a half-configured credential
/// pair throws <see cref="InvalidOperationException"/> naming the key.
/// </summary>
public static class S3StorageOptionsBinder
{
    public static S3StorageOptions Bind(
        IConfiguration configuration,
        string sectionPath,
        string accessKeyName,
        string secretKeyName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(sectionPath);

        var endpoint = Required(section, sectionPath, "Endpoint");
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
            (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"Media storage option '{sectionPath}:Endpoint' must be an absolute http(s) URL but was '{endpoint}'.");
        }

        var bucket = Required(section, sectionPath, "Bucket").Trim('/');
        if (bucket.Length == 0 || bucket.Contains('/'))
        {
            throw new InvalidOperationException(
                $"Media storage option '{sectionPath}:Bucket' must be a single bucket name without '/'.");
        }

        var publicBaseUrl = Optional(section, "PublicBaseUrl");
        if (publicBaseUrl is not null &&
            (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var publicUri) ||
             (publicUri.Scheme != Uri.UriSchemeHttp && publicUri.Scheme != Uri.UriSchemeHttps)))
        {
            throw new InvalidOperationException(
                $"Media storage option '{sectionPath}:PublicBaseUrl' must be an absolute http(s) URL but was '{publicBaseUrl}'.");
        }

        var forcePathStyle = true;
        var forcePathStyleRaw = Optional(section, "ForcePathStyle");
        if (forcePathStyleRaw is not null && !bool.TryParse(forcePathStyleRaw, out forcePathStyle))
        {
            throw new InvalidOperationException(
                $"Media storage option '{sectionPath}:ForcePathStyle' must be true or false but was '{forcePathStyleRaw}'.");
        }

        var accessKey = Normalize(configuration[accessKeyName]);
        var secretKey = Normalize(configuration[secretKeyName]);
        if ((accessKey is null) != (secretKey is null))
        {
            throw new InvalidOperationException(
                $"Media storage credentials are half-configured: '{accessKeyName}' and '{secretKeyName}' must both be set " +
                "(credentialed access) or both be empty (anonymous access).");
        }

        return new S3StorageOptions
        {
            Endpoint = endpoint.TrimEnd('/'),
            Bucket = bucket,
            Region = Optional(section, "Region") ?? "us-east-1",
            ForcePathStyle = forcePathStyle,
            PublicBaseUrl = publicBaseUrl,
            AccessKey = accessKey,
            SecretKey = secretKey
        };
    }

    private static string Required(IConfigurationSection section, string sectionPath, string name)
        => Optional(section, name)
           ?? throw new InvalidOperationException(
               $"Media storage option '{sectionPath}:{name}' is required but is not configured.");

    private static string? Optional(IConfigurationSection section, string name) => Normalize(section[name]);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
