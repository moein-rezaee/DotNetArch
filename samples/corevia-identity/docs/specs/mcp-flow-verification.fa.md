# گزارش اعتبارسنجی فلو MCP در IdentityService

[English](./mcp-flow-verification.md) | [فهرست Specها](./README.fa.md)

## هدف

ثبت فلو سطح سرویس MCP و اولین اعتبارسنجی Runtime اجرایی مطابق استاندارد Corevia، فقط برای `IdentityService`.

## دامنه

- مخزن تحت آزمون: `corevia-market`
- سرویس تحت آزمون: `IdentityService`
- نقطه ورود: `.corevia/operations/identity-service-mcp-create-or-migrate.values.yaml`
- نقطه ورود استاندارد: `mcp-service.create-or-migrate`
- در این اعتبارسنجی هیچ سرویس دیگری از Market تغییر یا مهاجرت داده نشد.

## اصلاح انجام‌شده

Manifest مربوط به MCP Identity قبلاً از نام‌های مفهومی برای Entry Pointهای Application استفاده می‌کرد. این نام‌ها به قراردادهای واقعی Query/Command اصلاح شدند:

- `GetProfileQuery`
- `GetCurrentUserSessionsQuery`
- `GetUserByIdQuery`
- `GetUserRolesQuery`
- `GetUserTenantsQuery`
- `RevokeSessionCommand`
- `RevokeOtherSessionsCommand`

وجود همه مسیرهای اعلام‌شده در درخت Application مربوط به Identity بررسی شد.

## ماتریس اعتبارسنجی

| بررسی | نتیجه | شواهد |
|---|---|---|
| اعتبارسنجی Facade سرویس | PASS | به‌دلیل Manifest فعال و وجود پروژه `IdentityService/IdentityService.Mcp`، مسیر `mcp-source.migrate-or-adopt` انتخاب می‌شود؛ Source Values همچنان Profile حاکم سرویس است. |
| قرارداد Manifest MCP | PASS | Modeها، Scopeها، Toolها، Transport، Audit، Redaction و مرزها معتبرند. |
| قرارداد Source MCP | PASS | Sibling، ورود فقط از Application، نبود API mirror، نبود دسترسی مستقیم به داده و Profileهای استقرار معتبرند. |
| قرارداد Roadmap Identity | PASS | Roadmap دوزبانه و Values فاز معتبرند. |
| Plan اولین فاز باز | PASS | Phase 7 (تست، مستندات و آمادگی Release) را گزارش می‌کند؛ فازهای ۱ تا ۶ شواهد اجرایی دارند. |
| وضعیت Roadmap | PASS | تصمیم فنی جدیدی لازم نیست. |
| بررسی Drift Skill تولیدی | PASS (محدود به دامنه) | `corevia-mcp-service-create-or-migrate` جاری و تولیدشده از Pattern است. بررسی All-pattern در Workspace مربوط به Standards به‌دلیل تغییرات نامرتبط ایجنت دیگر، چند Skill تولیدی قدیمی را Stale گزارش می‌کند و نتیجه Release این MR نیست. |
| اعتبارسنجی Skill تولیدی | PASS (محدود به دامنه) | Metadata و فایل‌های Skill انتخاب‌شده معتبرند؛ Drift نامرتبط Standards خارج از این تغییر Identity نگه داشته شده است. |
| Fixture شاخه Migration | PASS | Facade برای Source فعال، `mcp-source.migrate-or-adopt` را انتخاب می‌کند. |
| گیت Apply | BLOCK مورد انتظار | Facade سرویس همچنان read-only است و Mutation Source از مسیر میان‌بر `apply` انجام نمی‌شود. |
| شواهد پیاده‌سازی Runtime | PASS | پروژه Sibling Build شده، Suite کامل Catalog و Security موفق است و Smokeهای `initialize`/`tools/list` روی stdio با ۳۶ Tool فعال (۳۴ Atomic و ۲ Business) تمام شده‌اند؛ Smoke منفی HTTP روی Loopback نیز موفق است. |
| برابری مجوز API/MCP | PASS | ۴۱ ورودی قابلیت API Mapping یک‌به‌یک دارند: ۳۶ مجوز Authorization به Tool فعال و قابل‌استفاده expose شده‌اند و ۵ عملیات پروتکلی Binding صریح غیرقابل‌Expose برای Inventory دارند. Grantهای API/MCP جدا هستند و Drift به‌صورت Fail-closed رد می‌شود. |
| گیت خارجی Release | PENDING | CI/MR راه‌دور، Pilot مشتری، انتشار، Deploy Production و Gateway مدیریتی آینده گیت‌های جدا هستند. |

## مرز اعتبار گزارش

این گزارش فلو حاکمیت/مسیریابی، Contract اختصاصی Identity و Baseline Runtime محلی تأییدشده `IdentityService/IdentityService.Mcp` را پوشش می‌دهد. تکمیل CI/MR راه‌دور، پذیرش Transport مشتری، انتشار Package، Deploy Production، وجود Gateway مرکزی یا Federation سازمانی را ادعا نمی‌کند. Implementation Runtime، Rollout مشتری و تغییرات خارجی همچنان گیت‌های کنترل‌شده جدا هستند.

شواهد Runtime در [شواهد پیاده‌سازی Runtime MCP](./mcp-runtime-implementation-evidence.fa.md) نگهداری می‌شود.

## اقدام بعدی

بررسی‌های باقی‌مانده Phase 7، به‌خصوص CI/MR راه‌دور و اعتبارسنجی Repository/مستندات، را از مسیر `roadmap-phase.execute` کامل کنید. فقط پس از موفقیت آن‌ها Phase 7 را بسته و Phase 8 را برای Pilot جداگانه و تأییدشده باز کنید. برای دور زدن گیت، `apply` را روی Facade سرویس اجرا نکنید.

## سابقه تغییر

- 2026-08-28: ثبت اعتبارسنجی Facade فقط برای Identity و اصلاح نگاشت Entry Pointهای Application.
- 2026-08-29: گزارش با Catalog برابری ۴۱‌تایی API/MCP، Smoke پروتکل ۳۶ Tool فعال، Bindingهای صریح غیرقابل‌Expose عملیات پروتکلی و گیت‌های باقی‌مانده Phase 7 هم‌راستا شد.

## مالکیت

تیم IdentityService
