[English](./README.md)

# {{Name}}

فراداده:
- آخرین به‌روزرسانی: {{Date}}
- مالک: Platform Engineering
- وضعیت: فعال
- نسخه: 1.0.0

## Purpose
{{DescriptionFa}}

## Scope
- `Corevia.Kit.{{Name}}.Abstractions`: پورت‌ها و قراردادهای داده.
- `Corevia.Kit.{{Name}}.Core`: رجیستری، انتخاب و ثبت در DI.
<!-- dotnet-arch:providers -->

خارج از دامنه: هر چیزی که خود سرویس تصمیم می‌گیرد (قواعد کسب‌وکار، ذخیره‌سازی).

## Prerequisites
- .NET 9.

## Configuration
هر بسته بخش تنظیم خودش را در `docs/specs/contracts.md` مستند می‌کند.

## Run / Usage
```csharp
services.AddCorevia{{Name}}(configuration);
```

## Replaces
کپی‌هایی از این کد که سرویس‌ها محلی داشتند.

## Troubleshooting
- کدهای خطا در `docs/specs/contracts.md` آمده است.

## Validation / Verification
- `dotnet test tests/{{Name}}` (xUnit، بدون سرویس بیرونی).
- `ruby .gitlab/ci/validate.rb documentation`.

## Change Log
- {{Date}}: ناحیهٔ بستهٔ اولیه (۱.۰.۰).

## Ownership
- Owner: Platform Engineering
