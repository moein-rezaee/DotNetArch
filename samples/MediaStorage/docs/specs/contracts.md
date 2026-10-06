[فارسی](./contracts.fa.md)

# MediaStorage Contracts Spec

## Public API
- `MediaStorageProviderKind` (`Minio`, `RustFs`) - Abstractions
- `IMediaStorage`: `Provider`, `ListAsync(prefix, ct)`, `ListKeysAsync(prefix, ct)`, `GetInfoAsync(key, ct)`, `ExistsAsync(key, ct)`, `GetAsync(key, ct)`, `PutAsync(key, stream, contentType, ct)`, `DeleteAsync(key, ct)`, `GetPublicUrl(key)` - Abstractions
- `MediaObjectInfo(Key, Size, LastModified, ETag, ContentType)`, `MediaObject` (`Info`, `Content`, disposable) - Abstractions
- `MediaObjectNotFoundException` (Kit `NotFoundException`, error code `media_object_not_found`), `MediaStorageException` (Kit `ExternalServiceException`) - Abstractions
- `S3StorageOptions`, `S3StorageOptionsBinder.Bind(configuration, sectionPath, accessKeyName, secretKeyName)`, `S3ClientFactory.Create`, `S3CompatibleMediaStorage`, `IMediaStorageProvider` (`Kind`, `Create(IConfiguration)`), `MediaStorageProviderResolver`, `AddCoreviaMediaStorage(this IServiceCollection, IConfiguration)` - Core
- `AddCoreviaMinioProvider()`, `AddCoreviaRustFsProvider()`, `MinioMediaStorageProvider`, `RustFsMediaStorageProvider` - providers

## Configuration Contract
- `MediaStorage:Provider`: `Minio` (default) or `RustFs`, case-insensitive; unknown value throws `InvalidOperationException` listing supported values.
- Per provider section `MediaStorage:Minio` / `MediaStorage:RustFs`: `Endpoint` (required, absolute http/https), `Bucket` (required, single name), `PublicBaseUrl` (optional absolute http/https), `Region` (default `us-east-1`), `ForcePathStyle` (default `true`).
- Secrets, UPPER_CASE only (env/Vault): `MINIO_ACCESS_KEY`/`MINIO_SECRET_KEY`, `RUSTFS_ACCESS_KEY`/`RUSTFS_SECRET_KEY`. Both set = credentialed; both empty = anonymous; exactly one = `InvalidOperationException`. Secrets are never read from the section and never logged.
- Consul storage uses slash-separated kebab-case; runtime keys are colon-separated PascalCase.

## Error Contract
- Missing object on `GetAsync` -> `MediaObjectNotFoundException` (404). `GetInfoAsync` -> `null`, `ExistsAsync` -> `false`, `DeleteAsync` -> success.
- 401/403 -> `MediaStorageException` `media_storage_access_denied` (502); `NoSuchBucket` -> `media_storage_bucket_not_found` (502); other 4xx/5xx S3 errors -> `media_storage_error` (502); transport failure/timeout -> `media_storage_unavailable` (503).
- Caller cancellation surfaces as `OperationCanceledException`, not as a storage exception.

## Behavior Contract
- Keys are bucket-relative; leading `/` is ignored; blank keys throw `ArgumentException`.
- `GetPublicUrl` = trimmed `PublicBaseUrl` + `/` + each key segment URL-escaped; it never uses the internal `Endpoint`. No cache-busting query is added (consumer concern).
- Listing follows `NextContinuationToken` while truncated; a truncated page without a token ends the listing.
