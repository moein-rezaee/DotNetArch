[English](./identity-service-repo-migration-roadmap.md) | [والد](../roadmap.fa.md) | [اسپک Identity](../../specs/README.fa.md) | [رودمپ MCP](./identity-service-mcp-roadmap.fa.md)

# نقشه‌راه مخزن Corevia Identity

وضعیت: فعال (پایلوت مهاجرت مخزن سرویس)
تاریخ ایجاد: 2026-10-04

این سند نقشه‌راه مخزن `corevia-identity` است و از [رودمپ اصلی تجزیه Market](https://gitlab.getcorevia.ir/corevia/market/-/blob/develop/docs/roadmaps/services/market-decomposition-master-roadmap.md) (فاز C) جدا شده است. فقط موارد مربوط به همین سرویس اینجا نگهداری می‌شود. تاریخچه MCP در [رودمپ MCP](./identity-service-mcp-roadmap.fa.md) است.

## Endpoint / Definition of Done

Identity فقط از همین مخزن build، test، package و deploy می‌شود (Kit به‌صورت package)، با specs، agent-context، MCP sibling، pipeline فقط-CI، قالب values برای CD (`service-artifact.deploy`) و مجموعه تست کامل.

## مراحل فاز C

| مرحله | دامنه | وضعیت |
| --- | --- | --- |
| 2 | استخراج از مونورپو (`docs/evidence/monorepo-extraction-file-diff.md`) | انجام شد |
| 3-4 | مهاجرت Kit و رساندن Kit drift به 0 خطا | انجام شد |
| 5a | جداسازی Specs (`repo-specs.create-or-update`) | انجام شد |
| 5b | جداسازی agent-context (`repo-agent-context.create-or-update`) | انجام شد |
| 7 | MCP sibling: اعتبارسنج‌های contract و permission-parity | انجام شد (اعتبارسنج‌های contract و parity موفق؛ Transport پیش‌فرض به http اصلاح شد) |
| 8 | تفکیک CI/CD (فایل `.gitlab-ci.yml` فقط-CI و قالب CD) | انجام شد (docker build محلی اجرا نشد چون Docker daemon فعال نبود؛ `dotnet publish` تأیید شد) |
| 9 | ممیزی و تکمیل پوشش تست | انجام شد (۲۲۴ تست؛ `connectcore service-tests validate` موفق) |
| 10 | حذف IdentityService از مونورپو | گیت انسانی؛ در این کار انجام نمی‌شود |

## پیگیری‌های ثبت‌شده

- انجام شد: `Corevia.Kit.*` روی Nexus منتشر شد و هر `CoreviaKitPackagesRoot` project-reference با `PackageReference` جایگزین شد (نگاه کنید به `docs/specs/changelog.md` نسخه 0.11.17)؛ `Directory.Build.props` حذف شد. باقی‌مانده: `.gitlab-ci.yml` (مدیریت‌شده توسط `service-cicd.split`) همچنان بلوک `service_cicd.kit_stopgap` را برای checkout کردن `corevia-kit` در CI اجرا می‌کند — اکنون بی‌خطر اما نیازمند `service_cicd.kit_stopgap.enabled: false` در `corevia-standards/patterns/service-cicd.split/values.corevia-identity.yaml` و سپس `corevia-run apply --approve` برای حذف آن است.
- هیچ مقدار secret در این مخزن نگهداری نمی‌شود؛ منبع، Vault/Consul است.
