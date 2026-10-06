[English](./README.md)

# Corevia Kit MediaStorage

## هدف
دسترسی مستقل از ارائه‌دهنده به ذخیره‌ساز فایل‌های رسانه/آبجکت هر استقرار. سرویس‌ها فقط به `IMediaStorage`
(لیست کلیدها با prefix، head/exists، get، put، delete و ساخت URL عمومی یک کلید) وابسته‌اند و هرگز مستقیم با
MinIO، RustFS یا S3 صحبت نمی‌کنند. اینکه یک استقرار از کدام ذخیره‌ساز استفاده کند تصمیم پیکربندی است
(`MediaStorage:Provider` برابر `Minio` یا `RustFs`، پیش‌فرض `Minio`) نه تصمیم کد.

## جایگزین چه چیزی است
کد دست‌ساز MinIO در CatalogService (`MinioObjectListClient`، `MinioOptions` و ساخت URL عمومی داخل `ProductMediaResolver`):
فراخوانی `ListObjectsV2` روی HTTP خام، مدیریت bucket و prefix و تفکیک آدرس داخلی از آدرس عمومی. TTL کش و ساختار prefix
هر محصول دغدغه سرویس است و در سرویس می‌ماند. signature مربوط به `MediaStorage` در Kit-drift هر استفاده مستقیم جدید از
`Minio`/`AWSSDK.S3` در درخت سرویس را پرچم می‌زند.

## محدوده
انتخاب پکیج، پیکربندی، ثبت در DI، رفتار خطا و سازگاری این area. URL امضاشده (presigned)، تنظیمات multipart/resumable و
مدیریت bucket خارج از محدوده است.

## پکیج‌ها
- `Corevia.Kit.MediaStorage.Abstractions` نسخه 1.0.0 - `IMediaStorage`، recordها و استثناهای تایپ‌دار (نگاشت‌شده به `Corevia.Kit.ErrorHandling`).
- `Corevia.Kit.MediaStorage.Core` نسخه 1.0.0 - bind و اعتبارسنجی options، انتخاب provider، پیاده‌سازی مشترک S3-compatible (AWSSDK.S3) و `AddCoreviaMediaStorage`.
- `Corevia.Kit.MediaStorage.Providers.Minio` نسخه 1.0.0
- `Corevia.Kit.MediaStorage.Providers.RustFs` نسخه 1.0.0

جهت وابستگی: Providers.* -> Core -> Abstractions (Core هرگز به provider ارجاع نمی‌دهد). منطق S3 فقط یک‌بار در Core نوشته شده؛
هر provider فقط بخش پیکربندی، نام secretها و ثبت خودش را دارد.

## پیش‌نیازها
- لایه‌های Application/Domain فقط به `Corevia.Kit.MediaStorage.Abstractions` ارجاع می‌دهند.
- composition root به `Corevia.Kit.MediaStorage.Core` و هر providerی که سرویس ممکن است با آن مستقر شود (`Providers.Minio`، `Providers.RustFs`) ارجاع می‌دهد.
- یک ذخیره‌ساز S3-compatible در دسترس و یک bucket. اعتبارنامه اختیاری است (دسترسی ناشناس به bucket عمومی کار می‌کند).

## پیکربندی
غیرحساس (Consul/appsettings با کلیدهای PascalCase و دونقطه؛ در Consul به‌صورت kebab-case با اسلش):

| کلید | معنی |
| --- | --- |
| `MediaStorage:Provider` | `Minio` (پیش‌فرض در صورت نبودن کلید) یا `RustFs` بدون حساسیت به حروف؛ هر مقدار دیگر در استارت‌آپ خطا می‌دهد. |
| `MediaStorage:<Provider>:Endpoint` | الزامی. آدرس داخلی سرویس برای فراخوانی API، مثلا `http://minio:9000`. |
| `MediaStorage:<Provider>:Bucket` | الزامی. نام یک bucket. |
| `MediaStorage:<Provider>:PublicBaseUrl` | فقط در صورت استفاده از `GetPublicUrl` الزامی است. آدرس پایه‌ی قابل دسترس مرورگر (اگر پروکسی مسیر bucket را هم نشان می‌دهد آن را هم بیاورید)؛ متفاوت از `Endpoint`. |
| `MediaStorage:<Provider>:Region` | اختیاری، ناحیه‌ی امضا، پیش‌فرض `us-east-1`. |
| `MediaStorage:<Provider>:ForcePathStyle` | اختیاری، پیش‌فرض `true` (MinIO و RustFS لازم دارند). |

`<Provider>` برابر `Minio` یا `RustFs` است. هیچ پیش‌فرض ثابتی برای endpoint و bucket وجود ندارد.

Secretها (Vault یا متغیر محیطی، نام UPPER_CASE، هرگز در appsettings یا بخش غیرحساس):

| Provider | Access key | Secret key |
| --- | --- | --- |
| Minio | `MINIO_ACCESS_KEY` | `MINIO_SECRET_KEY` |
| RustFs | `RUSTFS_ACCESS_KEY` | `RUSTFS_SECRET_KEY` |

تنظیم هر دو یعنی دسترسی با اعتبارنامه (امضای SigV4)؛ خالی بودن هر دو یعنی دسترسی ناشناس (بدون امضا) که شیوه‌ی فعلی خواندن
bucket عمومی است. تنظیم فقط یکی از جفت در استارت‌آپ خطا می‌دهد و نام هر دو کلید را می‌آورد.

## اجرا و استفاده
```csharp
// composition root
services.AddCoreviaMinioProvider().AddCoreviaRustFsProvider();
services.AddCoreviaMediaStorage(configuration);   // provider را از MediaStorage:Provider انتخاب می‌کند

// هرجا (لایه‌ی Application فقط abstraction را می‌بیند)
public sealed class Resolver(IMediaStorage storage)
{
    public async Task<string[]> UrlsAsync(string prefix, CancellationToken ct) =>
        (await storage.ListKeysAsync(prefix, ct)).Select(storage.GetPublicUrl).ToArray();
}
```
`IMediaStorage`: `ListAsync`/`ListKeysAsync` (صفحه‌بندی درون provider)، `GetInfoAsync`/`ExistsAsync`، `GetAsync` (stream؛ آن را dispose کنید)،
`PutAsync(key, stream, contentType)`، `DeleteAsync` (idempotent)، `GetPublicUrl(key)`. هر عضو I/O یک `CancellationToken` می‌گیرد.

## رفتار خطا
- optionهای ناقص/نامعتبر `InvalidOperationException` با نام کلید می‌دهند؛ نه fallback بی‌صدا و نه پیش‌فرض برای کلید الزامی.
- `MediaStorage:Provider` ناشناخته در `AddCoreviaMediaStorage` خطا می‌دهد؛ providerِ انتخاب‌شده‌ی ثبت‌نشده در اولین resolve شدن `IMediaStorage` خطا می‌دهد.
- `MediaObjectNotFoundException` (از نوع `NotFoundException` در Kit، کد 404) برای شیء ناموجود در `GetAsync`.
- `MediaStorageException` (از نوع `ExternalServiceException` در Kit) با `ErrorCode` برابر `media_storage_access_denied` (401/403)، `media_storage_bucket_not_found`، `media_storage_error` (سایر خطاهای S3) یا `media_storage_unavailable` (خطای انتقال، 503).
- `GetInfoAsync` برای شیء ناموجود `null` و `ExistsAsync` مقدار `false` برمی‌گرداند؛ `GetPublicUrl` اگر `PublicBaseUrl` تنظیم نشده باشد خطا می‌دهد.
- stream غیرقابل seek در `PutAsync` ابتدا در حافظه بافر می‌شود؛ برای اشیای بزرگ stream قابل seek بدهید.

## سازگاری
area جدید، اولین انتشار 1.0.0؛ namespace عمومی قبلی برای حفظ سازگاری وجود ندارد. Core به `AWSSDK.S3` نسخه 4.0.104.1 وابسته است.

## اعتبارسنجی
- `dotnet build Corevia.Kit.packages.proj` و `dotnet test tests/MediaStorage/Corevia.Kit.MediaStorage.Tests`.
- `ruby .gitlab/ci/validate.rb documentation` و `COREVIA_VERSION_BASE_REF=origin/develop ruby .gitlab/ci/validate.rb version`.
- با پاسخ‌های HTTP شبیه‌سازی‌شده‌ی S3 راستی‌آزمایی شده است (صفحه‌بندی لیست، SigV4 در برابر ناشناس، ساخت URL، نگاشت خطا، انتخاب provider، options). روی سرور زنده‌ی MinIO یا RustFS راستی‌آزمایی نشده؛ پیش از اتکا به RustFS یک تست یکپارچگی در محیط هدف اجرا کنید.

## عیب‌یابی
- خطوط کنسول `[Corevia.Kit.MediaStorage]` در استارت‌آپ را ببینید: provider، endpoint، bucket و ناشناس/با اعتبارنامه بودن را نشان می‌دهد (هرگز کلیدها را نه).
- `media_storage_access_denied`: `*_ACCESS_KEY`/`*_SECRET_KEY` اشتباه یا نبود آن‌ها، یا policy که bucket عمومی نیست.
- `media_storage_bucket_not_found` یا 404 عجیب: `Bucket` اشتباه، یا `ForcePathStyle` برابر `false` برای ذخیره‌سازی که با path آدرس‌دهی می‌شود.
- URL عمومی اشتباه: `PublicBaseUrl` باید آدرس قابل دسترس مرورگر باشد نه `Endpoint` داخلی.

## مشخصات
- [مرور کلی](./docs/specs/overview.fa.md)
- [قراردادها](./docs/specs/contracts.fa.md)
- [پذیرش](./docs/specs/acceptance.fa.md)
- [تغییرات](./docs/specs/changelog.fa.md)

## تاریخچه تغییرات
- 1.0.0: پیاده‌سازی اولیه‌ی `Corevia.Kit.MediaStorage.{Abstractions,Core,Providers.Minio,Providers.RustFs}`.

## مالکیت
- مهندسی پلتفرم
