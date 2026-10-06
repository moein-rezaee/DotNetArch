[فارسی](./README.fa.md)

# Corevia Kit MediaStorage

## Purpose
Provider-neutral access to a deployment's media/object-file store. Services depend only on
`IMediaStorage` (list keys by prefix, head/exists, get, put, delete, build the public URL of a key)
and never talk to MinIO, RustFS or S3 directly. Which object store a deployment uses is a
configuration decision (`MediaStorage:Provider` = `Minio` | `RustFs`, default `Minio`), not a code decision.

## Replaces
The hand-rolled MinIO code in CatalogService (`MinioObjectListClient`, `MinioOptions`, the public-URL
building inside `ProductMediaResolver`): S3 `ListObjectsV2` over raw HTTP, bucket and prefix handling,
internal base URL versus public base URL. Cache TTL and the per-product prefix layout are service
concerns and stay in the service. The Kit-drift `MediaStorage` signature flags any new direct
`Minio`/`AWSSDK.S3` usage in a service tree.

## Scope
Package selection, configuration, registration, failure behavior and compatibility for this area.
Presigned URLs, multipart/resumable upload tuning and bucket administration are out of scope.

## Packages
- `Corevia.Kit.MediaStorage.Abstractions` v1.0.0 - `IMediaStorage`, records, typed exceptions (mapped to `Corevia.Kit.ErrorHandling`).
- `Corevia.Kit.MediaStorage.Core` v1.0.0 - options binding/validation, provider selection, the shared S3-compatible implementation (AWSSDK.S3) and `AddCoreviaMediaStorage`.
- `Corevia.Kit.MediaStorage.Providers.Minio` v1.0.0
- `Corevia.Kit.MediaStorage.Providers.RustFs` v1.0.0

Dependency direction: Providers.* -> Core -> Abstractions (Core never references a provider).
The S3 logic is written once in Core; each provider only owns its config section, secret names and registration.

## Prerequisites
- Application/Domain layers reference only `Corevia.Kit.MediaStorage.Abstractions`.
- The composition root references `Corevia.Kit.MediaStorage.Core` plus every provider the service may be deployed with (`Providers.Minio`, `Providers.RustFs`).
- A reachable S3-compatible store and a bucket. Credentials are optional (anonymous access to a public bucket works).

## Configuration
Non-sensitive (Consul/appsettings, PascalCase colon keys; Consul storage is kebab-case slash keys):

| Key | Meaning |
| --- | --- |
| `MediaStorage:Provider` | `Minio` (default when absent) or `RustFs`, case-insensitive; any other value fails at startup. |
| `MediaStorage:<Provider>:Endpoint` | Required. Internal service endpoint used for API calls, for example `http://minio:9000`. |
| `MediaStorage:<Provider>:Bucket` | Required. Single bucket name. |
| `MediaStorage:<Provider>:PublicBaseUrl` | Optional unless `GetPublicUrl` is used. Browser-facing base URL (include the bucket path if the proxy exposes it that way); distinct from `Endpoint`. |
| `MediaStorage:<Provider>:Region` | Optional signing region, default `us-east-1`. |
| `MediaStorage:<Provider>:ForcePathStyle` | Optional, default `true` (required by MinIO and RustFS). |

`<Provider>` is `Minio` or `RustFs`. There are no hardcoded endpoint or bucket defaults.

Secrets (Vault or environment, UPPER_CASE names, never in appsettings or the non-sensitive section):

| Provider | Access key | Secret key |
| --- | --- | --- |
| Minio | `MINIO_ACCESS_KEY` | `MINIO_SECRET_KEY` |
| RustFs | `RUSTFS_ACCESS_KEY` | `RUSTFS_SECRET_KEY` |

Both set means credentialed (SigV4-signed) access; both empty means anonymous (unsigned) access, which is how
a public bucket is read today. Setting only one of the pair throws at startup naming both keys.

## Run / Usage
```csharp
// composition root
services.AddCoreviaMinioProvider().AddCoreviaRustFsProvider();
services.AddCoreviaMediaStorage(configuration);   // picks the provider from MediaStorage:Provider

// anywhere (Application layer sees only the abstraction)
public sealed class Resolver(IMediaStorage storage)
{
    public async Task<string[]> UrlsAsync(string prefix, CancellationToken ct) =>
        (await storage.ListKeysAsync(prefix, ct)).Select(storage.GetPublicUrl).ToArray();
}
```
`IMediaStorage`: `ListAsync`/`ListKeysAsync` (pagination handled inside), `GetInfoAsync`/`ExistsAsync`, `GetAsync`
(stream, dispose it), `PutAsync(key, stream, contentType)`, `DeleteAsync` (idempotent), `GetPublicUrl(key)`.
Every I/O member takes a `CancellationToken`.

## Failure Behavior
- Missing/invalid options throw `InvalidOperationException` naming the key; no silent fallback and no default for a missing required key.
- Unknown `MediaStorage:Provider` throws at `AddCoreviaMediaStorage`; a selected but unregistered provider throws on first resolve of `IMediaStorage`.
- `MediaObjectNotFoundException` (a Kit `NotFoundException`, 404) for a missing object on `GetAsync`.
- `MediaStorageException` (a Kit `ExternalServiceException`) with `ErrorCode` `media_storage_access_denied` (401/403), `media_storage_bucket_not_found`, `media_storage_error` (other S3 errors) or `media_storage_unavailable` (transport failure, 503).
- `GetInfoAsync` returns `null` and `ExistsAsync` returns `false` for a missing object; `GetPublicUrl` throws if `PublicBaseUrl` is not configured.
- A non-seekable stream passed to `PutAsync` is buffered in memory first; pass a seekable stream for large objects.

## Compatibility
New package area, first release 1.0.0; no prior public namespace to preserve. Core carries a dependency on `AWSSDK.S3` 4.0.104.1.

## Validation
- `dotnet build Corevia.Kit.packages.proj` and `dotnet test tests/MediaStorage/Corevia.Kit.MediaStorage.Tests`.
- `ruby .gitlab/ci/validate.rb documentation` and `COREVIA_VERSION_BASE_REF=origin/develop ruby .gitlab/ci/validate.rb version`.
- Verified against stubbed S3 HTTP responses (list pagination, SigV4 versus anonymous, URL building, error mapping, selection, options). It has NOT been verified against a live MinIO or RustFS server; run an integration check in the target environment before relying on it for RustFS.

## Troubleshooting
- Check the `[Corevia.Kit.MediaStorage]` console lines at startup: they show the resolved provider, endpoint, bucket and whether access is anonymous or credentialed (never the keys).
- `media_storage_access_denied`: wrong/missing `*_ACCESS_KEY`/`*_SECRET_KEY` or a bucket policy that is not public.
- `media_storage_bucket_not_found` or odd 404s: wrong `Bucket`, or `ForcePathStyle` set to `false` for a store addressed by path.
- Public URLs wrong: `PublicBaseUrl` must be the browser-facing address, not the internal `Endpoint`.

## Specs
- [Overview](./docs/specs/overview.md)
- [Contracts](./docs/specs/contracts.md)
- [Acceptance](./docs/specs/acceptance.md)
- [Changelog](./docs/specs/changelog.md)

## Change Log
- 1.0.0: Initial `Corevia.Kit.MediaStorage.{Abstractions,Core,Providers.Minio,Providers.RustFs}`.

## Ownership
- Platform Engineering
