using System.Runtime.CompilerServices;
using Amazon.S3;
using Amazon.S3.Model;
using Corevia.Kit.MediaStorage.Abstractions;

namespace Corevia.Kit.MediaStorage.Core;

/// <summary>
/// The single shared <see cref="IMediaStorage"/> implementation for every S3-compatible provider.
/// Provider packages (MinIO, RustFS) only supply options and a provider kind.
/// </summary>
public sealed class S3CompatibleMediaStorage : IMediaStorage, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly S3StorageOptions _options;

    public S3CompatibleMediaStorage(MediaStorageProviderKind provider, S3StorageOptions options, IAmazonS3 client)
    {
        Provider = provider;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public MediaStorageProviderKind Provider { get; }

    public async IAsyncEnumerable<MediaObjectInfo> ListAsync(
        string prefix,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var normalizedPrefix = (prefix ?? string.Empty).Trim().TrimStart('/');
        string? continuationToken = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ListObjectsV2Response response;
            try
            {
                response = await _client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = _options.Bucket,
                    Prefix = normalizedPrefix,
                    ContinuationToken = continuationToken
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                throw S3ErrorMapper.Map(ex, "list", null);
            }

            foreach (var item in response.S3Objects ?? [])
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    continue;
                }

                yield return new MediaObjectInfo(item.Key.Trim(), item.Size ?? 0, ToOffset(item.LastModified), item.ETag);
            }

            if (response.IsTruncated != true || string.IsNullOrWhiteSpace(response.NextContinuationToken))
            {
                yield break;
            }

            continuationToken = response.NextContinuationToken;
        }
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var keys = new List<string>();
        await foreach (var item in ListAsync(prefix, cancellationToken).ConfigureAwait(false))
        {
            keys.Add(item.Key);
        }

        return keys;
    }

    public async Task<MediaObjectInfo?> GetInfoAsync(string key, CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);
        try
        {
            var response = await _client.GetObjectMetadataAsync(
                new GetObjectMetadataRequest { BucketName = _options.Bucket, Key = key }, cancellationToken).ConfigureAwait(false);
            return new MediaObjectInfo(
                key, response.ContentLength, ToOffset(response.LastModified), response.ETag, response.Headers.ContentType);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var mapped = S3ErrorMapper.Map(ex, "head", key);
            if (mapped is MediaObjectNotFoundException)
            {
                return null;
            }

            throw mapped;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => await GetInfoAsync(key, cancellationToken).ConfigureAwait(false) is not null;

    public async Task<MediaObject> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);
        try
        {
            var response = await _client.GetObjectAsync(
                new GetObjectRequest { BucketName = _options.Bucket, Key = key }, cancellationToken).ConfigureAwait(false);
            var info = new MediaObjectInfo(
                key, response.ContentLength, ToOffset(response.LastModified), response.ETag, response.Headers.ContentType);
            return new MediaObject(info, response.ResponseStream, response);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw S3ErrorMapper.Map(ex, "get", key);
        }
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);
        ArgumentNullException.ThrowIfNull(content);
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type is required.", nameof(contentType));
        }

        MemoryStream? buffer = null;
        try
        {
            var input = content;
            if (!content.CanSeek)
            {
                buffer = new MemoryStream();
                await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                buffer.Position = 0;
                input = buffer;
            }

            await _client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                InputStream = input,
                ContentType = contentType,
                AutoCloseStream = false
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw S3ErrorMapper.Map(ex, "put", key);
        }
        finally
        {
            buffer?.Dispose();
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);
        try
        {
            await _client.DeleteObjectAsync(
                new DeleteObjectRequest { BucketName = _options.Bucket, Key = key }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var mapped = S3ErrorMapper.Map(ex, "delete", key);
            if (mapped is not MediaObjectNotFoundException)
            {
                throw mapped;
            }
        }
    }

    public string GetPublicUrl(string key)
    {
        key = NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            throw new InvalidOperationException(
                $"Cannot build a public URL: the public base URL of the '{Provider}' media storage provider " +
                "('PublicBaseUrl') is not configured.");
        }

        var path = string.Join("/", key.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
        return _options.PublicBaseUrl.TrimEnd('/') + "/" + path;
    }

    public void Dispose() => _client.Dispose();

    private static string NormalizeKey(string key)
    {
        var normalized = (key ?? string.Empty).Trim().TrimStart('/');
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Object key must not be empty.", nameof(key));
        }

        return normalized;
    }

    private static DateTimeOffset? ToOffset(DateTime? value)
        => value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
}
