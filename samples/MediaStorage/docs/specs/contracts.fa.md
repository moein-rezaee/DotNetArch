[English](./contracts.md)

# مشخصات قراردادهای MediaStorage

## API عمومی
- `MediaStorageProviderKind` (`Minio`، `RustFs`) - Abstractions
- `IMediaStorage`: `Provider`، `ListAsync(prefix, ct)`، `ListKeysAsync(prefix, ct)`، `GetInfoAsync(key, ct)`، `ExistsAsync(key, ct)`، `GetAsync(key, ct)`، `PutAsync(key, stream, contentType, ct)`، `DeleteAsync(key, ct)`، `GetPublicUrl(key)` - Abstractions
- `MediaObjectInfo(Key, Size, LastModified, ETag, ContentType)`، `MediaObject` (`Info`، `Content`، قابل dispose) - Abstractions
- `MediaObjectNotFoundException` (از `NotFoundException` در Kit، کد خطا `media_object_not_found`)، `MediaStorageException` (از `ExternalServiceException` در Kit) - Abstractions
- `S3StorageOptions`، `S3StorageOptionsBinder.Bind(configuration, sectionPath, accessKeyName, secretKeyName)`، `S3ClientFactory.Create`، `S3CompatibleMediaStorage`، `IMediaStorageProvider` (`Kind`، `Create(IConfiguration)`)، `MediaStorageProviderResolver`، `AddCoreviaMediaStorage(this IServiceCollection, IConfiguration)` - Core
- `AddCoreviaMinioProvider()`، `AddCoreviaRustFsProvider()`، `MinioMediaStorageProvider`، `RustFsMediaStorageProvider` - providerها

## قرارداد پیکربندی
- `MediaStorage:Provider`: `Minio` (پیش‌فرض) یا `RustFs` بدون حساسیت به حروف؛ مقدار ناشناخته `InvalidOperationException` با فهرست مقادیر پشتیبانی‌شده می‌دهد.
- بخش هر provider یعنی `MediaStorage:Minio` / `MediaStorage:RustFs`: `Endpoint` (الزامی، http/https مطلق)، `Bucket` (الزامی، یک نام)، `PublicBaseUrl` (اختیاری، http/https مطلق)، `Region` (پیش‌فرض `us-east-1`)، `ForcePathStyle` (پیش‌فرض `true`).
- secretها فقط با نام UPPER_CASE (env/Vault): `MINIO_ACCESS_KEY`/`MINIO_SECRET_KEY` و `RUSTFS_ACCESS_KEY`/`RUSTFS_SECRET_KEY`. هر دو تنظیم‌شده = با اعتبارنامه؛ هر دو خالی = ناشناس؛ فقط یکی = `InvalidOperationException`. secretها هرگز از section خوانده و هرگز لاگ نمی‌شوند.
- ذخیره در Consul به‌صورت kebab-case با اسلش و کلید runtime به‌صورت PascalCase با دونقطه است.

## قرارداد خطا
- شیء ناموجود در `GetAsync` -> `MediaObjectNotFoundException` (404). `GetInfoAsync` -> `null`، `ExistsAsync` -> `false`، `DeleteAsync` -> موفق.
- 401/403 -> `MediaStorageException` با `media_storage_access_denied` (502)؛ `NoSuchBucket` -> `media_storage_bucket_not_found` (502)؛ سایر خطاهای 4xx/5xx در S3 -> `media_storage_error` (502)؛ خطای انتقال/timeout -> `media_storage_unavailable` (503).
- لغو توسط فراخواننده به‌صورت `OperationCanceledException` ظاهر می‌شود نه استثنای storage.

## قرارداد رفتار
- کلیدها نسبت به bucket هستند؛ `/` ابتدایی نادیده گرفته می‌شود؛ کلید خالی `ArgumentException` می‌دهد.
- `GetPublicUrl` = `PublicBaseUrl` بدون اسلش انتهایی + `/` + هر بخش کلید با URL-escape؛ هرگز از `Endpoint` داخلی استفاده نمی‌کند. پارامتر cache-busting اضافه نمی‌شود (با مصرف‌کننده).
- لیست تا زمانی که truncated است `NextContinuationToken` را دنبال می‌کند؛ صفحه‌ی truncated بدون token لیست را تمام می‌کند.
