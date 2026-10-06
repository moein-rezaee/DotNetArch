# kit ‏{{Area}} - پذیرش

- الف۱. `dotnet build -c Release` بدون warning موفق است و kit بدون هیچ مخزن والدی build می‌شود.
- الف۲. با یک provider ثبت‌شده `Add{{Area}}Kit` بدون `{{Area}}:Provider` کار می‌کند؛ با چند provider نبودن کلید با ذکر `{{Area}}:Provider` خطا می‌دهد.
- الف۳. نام provider ناشناخته خطا می‌دهد و providerهای ثبت‌شده را فهرست می‌کند.
- الف۴. Abstractions پکیج ثالث ندارد و Core به هیچ provider ارجاع نمی‌دهد.
- الف۵. هیچ secretی در appsettings یا مخزن نیست و همهٔ کلیدهای secret در README مستند است.
- الف۶. `scripts/pack.sh` برای هر پکیج یک nupkg با نسخهٔ kit می‌سازد.
