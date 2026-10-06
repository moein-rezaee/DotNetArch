[فارسی](./acceptance.fa.md)

# MediaStorage Acceptance Spec

## Package Boundary
- Abstractions reference no Core/provider/AWS package; Core references Abstractions only among Kit packages; providers reference Core and Abstractions.
- Application/Domain reference only Abstractions; services contain no `Minio`/`AWSSDK.*` reference or S3 HTTP code.

## Registration
- No `MediaStorage:Provider` -> `Minio` is selected; `RustFs` (any case) selects RustFS; unknown value fails at `AddCoreviaMediaStorage` with a message naming the key and supported values.
- A selected but unregistered provider fails on first resolve of `IMediaStorage` naming the missing `AddCoreviaXxxProvider()` and package.
- Provider registration is idempotent.

## Options
- Missing `Endpoint`/`Bucket`, non-http(s) endpoint, bucket containing `/`, invalid `PublicBaseUrl`/`ForcePathStyle` all throw `InvalidOperationException` naming the key.
- Half-configured credentials throw naming both secret keys; no credentials means anonymous unsigned requests; credentials mean `AWS4-HMAC-SHA256` signed requests; MinIO secrets are not read by the RustFS provider.

## Runtime Behavior
- Listing follows continuation tokens, sends path-style `list-type=2` requests with the encoded prefix, and honors cancellation.
- Get/put/head/delete behave as in the contracts spec, including non-seekable put buffering and idempotent delete.
- Error mapping matches the contracts spec.

## Verification Evidence
- `tests/MediaStorage/Corevia.Kit.MediaStorage.Tests` exercises all of the above against stubbed S3 HTTP responses for both providers. No live MinIO/RustFS server was available, so live verification is explicitly NOT claimed.
