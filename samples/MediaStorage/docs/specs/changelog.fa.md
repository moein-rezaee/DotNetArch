[English](./changelog.md)

# مشخصات تغییرات MediaStorage

## 2026-10-06 (نسخه 1.0.0)
- انتشار اولیه‌ی `Corevia.Kit.MediaStorage.{Abstractions,Core,Providers.Minio,Providers.RustFs}` نسخه 1.0.0: `IMediaStorage`، پیاده‌سازی مشترک S3-compatible با AWSSDK.S3 در Core، انتخاب provider با پیکربندی (`MediaStorage:Provider`، پیش‌فرض `Minio`)، استثناهای نگاشت‌شده به ErrorHandling در Kit و افزودن signature مربوط به `MediaStorage` در Kit-drift در corevia-standards.

## نکته‌ی سازگاری
area جدید؛ namespace عمومی قبلی برای حفظ سازگاری وجود ندارد.
