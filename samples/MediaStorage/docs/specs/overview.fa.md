[English](./overview.md)

# مشخصات مرور کلی MediaStorage

## هدف
تعریف area با نام `Corevia.Kit.MediaStorage.*`: ذخیره‌سازی رسانه/فایل آبجکت مستقل از provider تا سرویس‌ها هرگز مستقیم با MinIO، RustFS یا S3 صحبت نکنند. provider برای هر استقرار در پیکربندی انتخاب می‌شود.

## مرز پکیج‌ها
- `Corevia.Kit.MediaStorage.Abstractions`
- `Corevia.Kit.MediaStorage.Core`
- `Corevia.Kit.MediaStorage.Providers.Minio` (provider پیش‌فرض)
- `Corevia.Kit.MediaStorage.Providers.RustFs`

## جهت وابستگی
Providers.* -> Core -> Abstractions. Abstractions فقط به `Corevia.Kit.ErrorHandling.Abstractions` ارجاع می‌دهد. Core فقط به Abstractions (به‌علاوه AWSSDK.S3 و Microsoft.Extensions.*) ارجاع می‌دهد و هرگز به provider. هر provider خودش را ثبت می‌کند (`AddCoreviaMinioProvider()` / `AddCoreviaRustFsProvider()`) مانند `packages/Cache` و `packages/DatabaseConnection`. لایه‌های Application فقط به Abstractions ارجاع می‌دهند؛ composition root به Core و providerهای مورد پشتیبانی.

## محدوده
- لیست کلیدهای آبجکت با prefix (continuation درونی)، head/exists، get (stream)، put (stream به‌همراه content type)، delete و ساخت URL عمومی.
- یک پیاده‌سازی مشترک S3-compatible در Core (AWSSDK.S3، `ServiceURL` و path-style)؛ providerها فقط options، نام secret و ثبت را می‌دهند.
- انتخاب با `MediaStorage:Provider` (پیش‌فرض `Minio`)؛ بدون اعتبارنامه دسترسی ناشناس، با اعتبارنامه دسترسی امضاشده SigV4.

## خارج از محدوده
- URL امضاشده (میزبان امضاشده آدرس داخلی می‌شد نه عمومی)، تنظیمات multipart، مدیریت bucket، پردازش تصویر، کش لیست‌ها (TTL و ساختار کلید با مصرف‌کننده است).
- ساختار prefix مخصوص سرویس مانند `product/<code>/` در Catalog.
- provider سوم بدون تصمیم مالک محصول.

## جایگزین چه چیزی است (قانون Kit-First)
`MinioObjectListClient`، `MinioOptions` و ساخت URL عمومی در `ProductMediaResolver` سرویس CatalogService (`ListObjectsV2` روی HTTP خام، مدیریت bucket/prefix، آدرس پایه‌ی داخلی در برابر عمومی). در `docs/specs/market-shared-documentation-migration-audit.md` ثبت شده است. signature مربوط به `MediaStorage` در Kit-drift استفاده‌ی مستقیم جدید از `Minio`/`AWSSDK.S3` را پرچم می‌زند.

## سازگاری
area جدید با نسخه 1.0.0.
