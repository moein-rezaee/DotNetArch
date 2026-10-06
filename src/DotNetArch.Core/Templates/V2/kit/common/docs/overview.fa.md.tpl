# kit ‏{{Area}} - مرور

{{AreaDescriptionFa}}

## دامنه
انتخاب پکیج، پیکربندی، ثبت، رفتار در خطا و سازگاری قابلیت {{Area}}.

## پکیج‌ها
{{ProviderPackageList}}
- `{{Prefix}}.Kit.{{Area}}.Abstractions` قراردادها؛ `{{Prefix}}.Kit.{{Area}}.Core` options و انتخاب و نقطهٔ ورود.

## قواعد طراحی
- `Providers.* -> Core -> Abstractions`؛ Core هرگز به provider ارجاع نمی‌دهد.
- provider با `{{Area}}:Provider` انتخاب می‌شود؛ افزودن provider کد مصرف‌کننده را تغییر نمی‌دهد.
- secretها فقط از کلیدهای UPPER_CASE محیط خوانده می‌شوند.
