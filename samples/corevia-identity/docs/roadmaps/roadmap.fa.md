[English](./roadmap.md)

# نقشه‌راه Identity

## Endpoint / Definition of Done

- مخزن به‌تنهایی build، test، package و deploy می‌شود: pipeline فقط-CI، قالب values برای CD (`service-artifact.deploy`)، مصرف Kit به‌صورت package و مجموعه تست کامل.
- OpenSpec و TestSpec زیر کنترل نسخه می‌مانند و پیش از کار پیاده‌سازی اعتبارسنجی می‌شوند.
- قوانین agent، اسپک‌ها و نقشه‌راه‌ها فقط مخصوص Identity و دوزبانه با لینک متقابل هستند.

## فاز فعلی

- [x] تکمیل OpenSpec و TestSpec مخصوص سرویس (مرحله 5a).
- [x] جداسازی agent-context و نقشه‌راه‌ها (مرحله 5b)، راستی‌آزمایی MCP sibling (مرحله 7)، تفکیک CI/CD (مرحله 8) و تکمیل مجموعه تست (مرحله 9).
- [x] انتشار Corevia.Kit.* در Nexus و تبدیل stopgap مبتنی بر project-reference هم‌ردیف به PackageReference.
- [ ] گیت انسانی: حذف IdentityService از مونورپوی corevia-market (مرحله 10).

برای وضعیت هر مرحله [نقشه‌راه مهاجرت مخزن](./services/identity-service-repo-migration-roadmap.fa.md) و برای همه مستندات [فهرست مستندات](../INDEX.fa.md) را ببینید.

## اعتبارسنجی

- اعتبارسنج اعلام‌شده در `.corevia/validators.yaml` و سپس `dotnet test IdentityService.sln` را اجرا کنید (checkout هم‌ردیف `corevia-kit` لازم است).
