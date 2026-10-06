[English](./README.md)

# {{Prefix}}.Kit.{{Area}}

## هدف
{{AreaDescriptionFa}} سرویس‌ها فقط به `I{{Area}}` (پکیج `{{Prefix}}.Kit.{{Area}}.Abstractions`) وابسته‌اند و هرگز مستقیم به SDK یک provider ارجاع نمی‌دهند.
انتخاب provider یک تصمیم پیکربندی است (`{{Area}}:Provider`) نه تصمیم کد.

## پکیج‌ها
- `{{Prefix}}.Kit.{{Area}}.Abstractions` - قراردادها، recordها و exceptionهای تایپ‌شده؛ بدون وابستگی ثالث.
- `{{Prefix}}.Kit.{{Area}}.Core` - bind و اعتبارسنجی options، انتخاب provider و تنها نقطهٔ ورود `Add{{Area}}Kit`.
{{ProviderPackageList}}
جهت وابستگی: `Providers.* -> Core -> Abstractions`؛ Core هرگز به provider ارجاع نمی‌دهد.

## پیش‌نیاز
- لایه‌های Application و Domain فقط به `{{Prefix}}.Kit.{{Area}}.Abstractions` ارجاع می‌دهند.
- composition root به `{{Prefix}}.Kit.{{Area}}.Core` و هر providerای که ممکن است استفاده شود ارجاع می‌دهد.

## استفاده
```csharp
{{RegistrationSnippet}}
services.Add{{Area}}Kit(configuration);   // provider از {{Area}}:Provider انتخاب می‌شود
```

## پیکربندی
کلیدهای غیرحساس (appsettings):

| کلید | معنی |
| --- | --- |
| `{{Area}}:Provider` | نام provider ({{ProviderNames}}). اگر بیش از یک provider ثبت شده باشد الزامی است. |
{{ConfigTable}}
secretها (environment / secret store، با حروف بزرگ، هرگز در appsettings):

| کلید | استفاده |
| --- | --- |
{{SecretTable}}
## رفتار در خطا
- options نامعتبر یا ناقص `InvalidOperationException` با نام کلید می‌دهد؛ fallback بی‌صدا وجود ندارد.
- `{{Area}}:Provider` ناشناخته در اولین resolve خطا می‌دهد و providerهای ثبت‌شده را فهرست می‌کند.
- خطاهای زمان اجرا با `{{Area}}Exception` (دارای `ErrorCode` پایدار) یا نوع مشتق از آن گزارش می‌شوند.

## build و انتشار
```bash
dotnet build -c Release
scripts/pack.sh
```
نسخهٔ kit مستقل است (`Version` در `Directory.Build.props`) و CI می‌تواند آن را جدا از سرویس‌ها build و pack و push کند.
