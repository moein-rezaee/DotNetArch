[فارسی](./changelog.fa.md)

# MediaStorage Changelog Spec

## 2026-10-06 (v1.0.0)
- Initial `Corevia.Kit.MediaStorage.{Abstractions,Core,Providers.Minio,Providers.RustFs}` 1.0.0: `IMediaStorage`, shared AWSSDK.S3 S3-compatible implementation in Core, config-selected provider (`MediaStorage:Provider`, default `Minio`), Kit ErrorHandling-mapped exceptions, Kit-drift `MediaStorage` signature added in corevia-standards.

## Compatibility Note
New package area; no prior public namespace to preserve compatibility with.
