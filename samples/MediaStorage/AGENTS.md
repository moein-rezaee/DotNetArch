# AGENTS - MediaStorage

Applies to `packages/MediaStorage/`.

## Spec Rule

- Keep package-area specs in `docs/specs/`.
- Any behavior or contract change must update `docs/specs/overview.md`, `docs/specs/contracts.md` and `docs/specs/acceptance.md` (and the `.fa.md` pairs).

## Architecture Rule

- Keep public contracts (`IMediaStorage`, records, exceptions, `MediaStorageProviderKind`) in `Corevia.Kit.MediaStorage.Abstractions`; it references only `Corevia.Kit.ErrorHandling.Abstractions`.
- Keep options binding/validation, provider selection, the shared S3-compatible implementation (`S3CompatibleMediaStorage`, AWSSDK.S3) and the single `AddCoreviaMediaStorage` entry point in `Corevia.Kit.MediaStorage.Core`. Core references only Abstractions, never a provider package.
- Keep each provider (`Providers.Minio`, `Providers.RustFs`) thin: its own config section, its own secret names, and its own `AddCoreviaXxxProvider()`. Never copy S3 logic into a provider; extend Core instead.
- Services must not reference `Minio`, `AWSSDK.*` or call S3 endpoints directly; the Kit-drift `MediaStorage` signature flags this.
- Secrets (`MINIO_*`/`RUSTFS_*` access and secret keys) are read only from UPPER_CASE configuration keys (env/Vault); never add them to the non-sensitive section or give endpoint/bucket hardcoded defaults.
- Do not add a third provider without an owner decision recorded in `docs/specs/market-shared-documentation-migration-audit.md`.

## Testing Rule

- Tests live in `tests/MediaStorage/` (outside `packages/` so they are not packed or version-validated) and use stubbed S3 HTTP responses. Do not claim live MinIO/RustFS verification unless it was actually run.

## Documentation Governance Rule

- Keep bilingual documentation pairs synchronized (`.md` + `.fa.md`); keep specs and changelog updated in the same change.
