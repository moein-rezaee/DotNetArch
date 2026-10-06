[English](./acceptance.md)

# مشخصات پذیرش MediaStorage

## مرز پکیج‌ها
- Abstractions به Core/provider/بسته‌ی AWS ارجاع نمی‌دهد؛ Core از میان پکیج‌های Kit فقط به Abstractions ارجاع می‌دهد؛ providerها به Core و Abstractions ارجاع می‌دهند.
- Application/Domain فقط به Abstractions ارجاع می‌دهند؛ سرویس‌ها ارجاع `Minio`/`AWSSDK.*` یا کد HTTP مربوط به S3 ندارند.

## ثبت در DI
- بدون `MediaStorage:Provider` مقدار `Minio` انتخاب می‌شود؛ `RustFs` (با هر حالت حروف) RustFS را انتخاب می‌کند؛ مقدار ناشناخته در `AddCoreviaMediaStorage` با پیامی شامل نام کلید و مقادیر پشتیبانی‌شده خطا می‌دهد.
- providerِ انتخاب‌شده‌ی ثبت‌نشده در اولین resolve شدن `IMediaStorage` با نام `AddCoreviaXxxProvider()` و پکیج مفقود خطا می‌دهد.
- ثبت provider idempotent است.

## Options
- نبود `Endpoint`/`Bucket`، endpoint غیر http(s)، bucket حاوی `/`، و `PublicBaseUrl`/`ForcePathStyle` نامعتبر همگی `InvalidOperationException` با نام کلید می‌دهند.
- اعتبارنامه‌ی نیمه‌کاره با نام هر دو secret خطا می‌دهد؛ بدون اعتبارنامه درخواست‌های ناشناس بدون امضا؛ با اعتبارنامه درخواست‌های امضاشده `AWS4-HMAC-SHA256`؛ secretهای MinIO توسط providerِ RustFS خوانده نمی‌شوند.

## رفتار زمان اجرا
- لیست continuation tokenها را دنبال می‌کند، درخواست path-style با `list-type=2` و prefix انکود‌شده می‌فرستد و لغو را رعایت می‌کند.
- get/put/head/delete مطابق مشخصات قراردادها رفتار می‌کنند، از جمله بافر شدن put غیرقابل seek و delete آیدمپوتنت.
- نگاشت خطا مطابق مشخصات قراردادهاست.

## شواهد راستی‌آزمایی
- `tests/MediaStorage/Corevia.Kit.MediaStorage.Tests` همه‌ی موارد بالا را برای هر دو provider با پاسخ‌های HTTP شبیه‌سازی‌شده‌ی S3 اجرا می‌کند. سرور زنده‌ی MinIO/RustFS در دسترس نبود، بنابراین راستی‌آزمایی زنده صراحتا ادعا نمی‌شود.
