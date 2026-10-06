[فارسی](./overview.fa.md)

# MediaStorage Overview Spec

## Goal
Define the `Corevia.Kit.MediaStorage.*` area: provider-neutral media/object-file storage so services never talk to MinIO, RustFS or S3 directly. The provider is chosen per deployment in configuration.

## Package Boundary
- `Corevia.Kit.MediaStorage.Abstractions`
- `Corevia.Kit.MediaStorage.Core`
- `Corevia.Kit.MediaStorage.Providers.Minio` (default provider)
- `Corevia.Kit.MediaStorage.Providers.RustFs`

## Dependency Direction
Providers.* -> Core -> Abstractions. Abstractions references only `Corevia.Kit.ErrorHandling.Abstractions`. Core references only Abstractions (plus AWSSDK.S3 and Microsoft.Extensions.*) and never a provider package. Each provider registers itself (`AddCoreviaMinioProvider()` / `AddCoreviaRustFsProvider()`), like `packages/Cache` and `packages/DatabaseConnection`. Application layers reference only Abstractions; the composition root references Core and the providers it supports.

## Scope
- List object keys by prefix (continuation handled internally), head/exists, get (stream), put (stream + content type), delete, public URL building.
- One shared S3-compatible implementation in Core (AWSSDK.S3, `ServiceURL` + path-style); providers contribute options, secret names and registration only.
- Selection by `MediaStorage:Provider` (default `Minio`); anonymous access when no credentials are configured, credentialed SigV4 access otherwise.

## Non-Goals
- Presigned URLs (the signed host would be the internal endpoint, not the public one), multipart tuning, bucket administration, image processing, caching of listings (the consumer owns TTL and key layout).
- Service-specific prefix layouts such as Catalog's `product/<code>/`.
- A third provider without an owner decision.

## Replaces (Kit-First Rule)
CatalogService's `MinioObjectListClient`, `MinioOptions` and the public-URL building in `ProductMediaResolver` (S3 `ListObjectsV2` over raw HTTP, bucket/prefix handling, internal versus public base URL). Recorded in `docs/specs/market-shared-documentation-migration-audit.md`. The Kit-drift `MediaStorage` signature flags new direct `Minio`/`AWSSDK.S3` use.

## Compatibility
New area at 1.0.0.
