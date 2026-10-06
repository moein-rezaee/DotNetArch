namespace {{Prefix}}.Kit.MediaStorage.Core;

/// <summary>
/// Validated settings of one S3-compatible object store (MinIO, RustFS, ...). Built by
/// <see cref="S3StorageOptionsBinder"/>; the access/secret keys are secrets and are never part of
/// the non-sensitive configuration section.
/// </summary>
public sealed class S3StorageOptions
{
    /// <summary>Internal service endpoint used for API calls (for example <c>http://minio:9000</c>).</summary>
    public string Endpoint { get; init; } = string.Empty;

    public string Bucket { get; init; } = string.Empty;

    /// <summary>Signing region; S3-compatible stores ignore it, SigV4 still needs a value.</summary>
    public string Region { get; init; } = "us-east-1";

    /// <summary>Path-style addressing (<c>endpoint/bucket/key</c>); required by MinIO and RustFS.</summary>
    public bool ForcePathStyle { get; init; } = true;

    /// <summary>
    /// Browser-facing base URL that already includes the bucket path when the proxy exposes it that way
    /// (for example <c>https://example.com/minio/nikplus/</c>). Distinct from <see cref="Endpoint"/>.
    /// Optional unless <c>GetPublicUrl</c> is used.
    /// </summary>
    public string? PublicBaseUrl { get; init; }

    /// <summary>Secret. Null together with <see cref="SecretKey"/> means anonymous (unsigned) access.</summary>
    public string? AccessKey { get; init; }

    /// <summary>Secret.</summary>
    public string? SecretKey { get; init; }

    public bool IsAnonymous => string.IsNullOrEmpty(AccessKey);
}
