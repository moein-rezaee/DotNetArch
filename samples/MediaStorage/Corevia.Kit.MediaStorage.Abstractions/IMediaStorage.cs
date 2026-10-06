namespace Corevia.Kit.MediaStorage.Abstractions;

/// <summary>
/// Provider-neutral access to the deployment's media/object-file store. Services depend only on this
/// contract and never talk to MinIO, RustFS or S3 directly. Keys are bucket-relative (a leading '/'
/// is ignored). Failures surface as <see cref="MediaObjectNotFoundException"/> or
/// <see cref="MediaStorageException"/>; cancellation surfaces as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IMediaStorage
{
    /// <summary>Provider selected by configuration for this deployment.</summary>
    MediaStorageProviderKind Provider { get; }

    /// <summary>Lists objects under <paramref name="prefix"/>; paging/continuation is handled internally.</summary>
    IAsyncEnumerable<MediaObjectInfo> ListAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>Lists all object keys under <paramref name="prefix"/> (all pages).</summary>
    Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>Returns the object's metadata, or <c>null</c> when it does not exist (HEAD).</summary>
    Task<MediaObjectInfo?> GetInfoAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Downloads an object; throws <see cref="MediaObjectNotFoundException"/> when missing.</summary>
    Task<MediaObject> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads <paramref name="content"/>. The stream is not closed. A non-seekable stream is buffered
    /// in memory first, so pass a seekable stream for large objects.
    /// </summary>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Deletes an object; deleting a missing object is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the browser-facing URL of <paramref name="key"/> from the configured public base URL
    /// (not the internal endpoint). Throws <see cref="InvalidOperationException"/> when no public
    /// base URL is configured.
    /// </summary>
    string GetPublicUrl(string key);
}
