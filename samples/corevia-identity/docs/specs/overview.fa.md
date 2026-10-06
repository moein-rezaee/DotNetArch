# نمای کلی مهاجرت Runtime در IdentityService

[English](./overview.md) | [فارسی](./overview.fa.md)

> وضعیت: Baseline پیاده‌سازی فعال؛ Pilot کنترل‌شده باقی‌مانده است
> نسخه: ۰.۱۱.۱
> مالک: IdentityService

## دامنه

- سرویس: `IdentityService`
- پورت پیش‌فرض: `5270`
- پیشوند API: `v1/api`
- هدف مهاجرت Runtime: انتقال کانفیگ به Consul، انتقال Secrets به Vault و Merge در Startup با `AddConfigLoaderExtension(...)`

## مسئولیت‌ها

- حفظ رفتار لاگین OTP و چرخه توکن/سشن حین مهاجرت مقادیر Runtime از فایل‌های محلی
- تضمین ترتیب Bootstrap برای Providerهای متمرکز
- حفظ رجیستری Service Discovery در Consul و سلامت `/health`
- seed کردن کلاینت‌های M2M اختصاصی `invoice-service` و `pos-service` برای جریان مالی POS
- حفظ قرارداد کلاینت M2M با شناسه `woosync-service` برای provider مستقل ووکامرس در `corevia-sync`

- برگرداندن نتیجه صریح پذیرفته‌شدن/صف‌شدن از endpoint ارسال OTP تا client موفقیت را از پاسخ خالی حدس نزند.

## وابستگی‌های Runtime

- Consul (`Corevia.Kit.Config`، فضای نام `ConfigCenterExtension`)
- Vault (`Corevia.Kit.Secrets`، فضای نام `SecretStoreExtension`)
- Config loader (`Corevia.Kit.ConfigLoader`؛ Providerهای Config و Secrets قبل از `AddConfigLoaderExtension(...)` ثبت می‌شوند)
- آداپتر Provider دیتابیس: Postgres به‌صورت پیش‌فرض؛ SQL Server از طریق تنظیمات Runtime قابل انتخاب است.
- OTPService

## ناوردای مهاجرت

- `AddConfigLoaderExtension(...)` قبل از هر Options Binding و قبل از ثبت سرویس‌های وابسته به کانفیگ اجرا می‌شود.
- `appsettings.json` فقط شامل مقادیر غیرحساس باقی می‌ماند.
- `.env` فقط شامل کلیدهای Bootstrap/اتصال Providerها و مقادیر غیرحساس محلی است.

## Profile استاندارد MCP در Corevia

معماری عمومی MCP، امنیت، ToolSpec، Transport، Migration، Compatibility، Rollback و Acceptance از [استاندارد ایجاد و مهاجرت سورس MCP در Corevia](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/README.fa.md) و [قراردادهای الزامی MCP](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/contracts.fa.md) به ارث می‌رسند. Profile ماشین‌خوان Identity در [`.corevia/mcp/identity-service.yaml`](../../.corevia/mcp/identity-service.yaml) است.

Profile اختصاصی Identity:

- `IdentityService.Api` و `IdentityService.Mcp` Adapterهای انتقال هم‌سطح روی Application/Domain هستند و MCP مستقیماً Use Case/Handler MediatR را فراخوانی می‌کند.
- Scopeهای Identity عبارت‌اند از `identity.mcp.self.read`، `identity.mcp.self.write`، `identity.mcp.admin.read`، `identity.mcp.admin.write`، `identity.mcp.security.read` و `identity.mcp.security.write`.
- `self` Context فعلی مشتری، `admin` Context داخلی و tenant-scoped اپراتور/Agent و `security` جدا و در MVP پیش‌فرض غیرفعال است. Delegation یک Context مشتری امضاشده و کوتاه‌مدت زیر Self است، Mode چهارم یا Escalation نیست.
- Identity Catalog کامل قابلیت فعلی را با هر دو نوع Tool ارائه می‌کند: ۳۴ Tool نوع Atomic و ۲ Tool نوع Business قابلیت‌های Profile/Session خود، User، Role، Tenant، Permission، Scope، Client و Relationship را پوشش می‌دهند. Proxy عمومی API/Endpoint/SQL/Repository ممنوع است.
- نصب Local مشتری از HTTP احراز‌شده Loopback (پیش‌فرض) استفاده می‌کند؛ `stdio` فقط برای توسعه است. نصب سرور مشتری با HTTPS احراز‌شده یا کنترل معادل شبکه خصوصی انجام می‌شود. Gateway مرکزی زیرساخت اختیاری آینده است.
- AI ابری فقط از طریق MCP Client/Desktop Connector مورد اعتماد محلی و با Egress مورد تأیید مشتری به MCP محلی می‌رسد. مسیر سازمانی آینده AD/LDAP → Keycloak User Federation → OIDC/OAuth2 است و MCP مستقیم به AD/LDAP Bind نمی‌شود.

نقطه ورود سطح سرویس [Values ایجاد یا مهاجرت MCP در Identity](../../.corevia/mcp/identity.values.yaml) است؛ این Values وضعیت Source را تشخیص می‌دهد و به Pattern فرزند حاکم هدایت می‌کند. ایجاد Source با [Values استاندارد ایجاد Source MCP در Identity](../../.corevia/operations/identity-service-mcp-source.values.yaml) هدایت می‌شود. Contract عمومی با [Values اعتبارسنجی MCP Identity](../../.corevia/operations/identity-service-mcp-contract.values.yaml) بررسی می‌شود و وضعیت اجرای واقعی در [شواهد پیاده‌سازی Runtime MCP](./mcp-runtime-implementation-evidence.fa.md) ثبت شده است. وضعیت Implementation با [رودمپ MCP IdentityService](../roadmaps/services/identity-service-mcp-roadmap.fa.md) کنترل می‌شود.

## پیاده‌سازی Runtime مربوط به MCP

اولین Baseline اجرایی MCP Identity اکنون در `IdentityService/IdentityService.Mcp` وجود دارد:

- `identity_get_self_profile` و `identity_list_self_sessions` Toolهای محدود Atomic و فقط‌خواندنی Self هستند.
- `identity_get_user_access_summary` یک Tool نوع Business و فقط‌خواندنی برای Admin است که Queryهای Application مربوط به User، Role و Tenant مجاز را هماهنگ می‌کند.
- `identity_revoke_user_sessions` یک Tool نوع Business و حساس برای Admin است که Approval امضاشده و دقیق می‌خواهد و Commandهای موجود Session را مستقیم فراخوانی می‌کند. سایر قابلیت‌های فعال Admin به‌صورت Toolهای Atomic نام‌دار روی Handlerهای متناظر Application expose شده‌اند.
- Runtime، Context اجرای ساخته‌شده توسط سرور، Resolve امن Actor/Subject/Tenant، Scopeهای جداگانه MCP، Policyهای Self/Admin/Delegated، مرز Tenant، Nonce ضد Replay، DTOهای Redacted، Audit و Binding Approval را enforce می‌کند.
- Transport پیش‌فرض HTTP است (Loopback احراز‌شده یا HTTPS/HTTP خصوصی کنترل‌شده سرور مشتری)؛ `stdio` فقط برای توسعه است و خارج از Development رد می‌شود. Host مربوط به stdio Listener کستل ایجاد نمی‌کند.
- Catalog قابلیت API/MCP شامل ۴۱ ورودی با Mapping یک‌به‌یک است: ۳۶ مجوز فعال Authorization به Tool فعال و قابل‌استفاده وصل‌اند و پنج عملیات پروتکلی به‌دلیل ممنوعیت افشای خام Token/Secret فقط برای Inventory و غیرقابل‌Expose هستند. Grantهای API و MCP جدا می‌مانند و Drift به‌صورت Fail-closed رد می‌شود.

Build محلی، Suite کامل Catalog/Security و Smoke پروتکل `initialize`/`tools/list` روی stdio در [شواهد پیاده‌سازی Runtime MCP](./mcp-runtime-implementation-evidence.fa.md) ثبت شده‌اند. اعتبارسنجی Remote CI/MR و Pilot کنترل‌شده مشتری همچنان گیت Release هستند.

## آخرین به‌روزرسانی

- 2026-07-06

- 2026-09-03

## Purpose

هدف اصلی و کاربرد این بخش یا ماژول.
## Scope

محدوده و بازه تاثیر این بخش.
## Prerequisites

پیش‌نیازهای لازم برای استفاده یا اجرای این بخش.
## Configuration

راهنمای تنظیمات و پیکربندی.
## Run / Usage

راهنمای اجرا و نحوه استفاده.
## Validation / Verification

فرآیند اعتبارسنجی و تصدیق.
## Troubleshooting

راهنمای حل مشکلات عام.
## Change Log

سابقه تغییرات و نسخه‌های این بخش.
## Ownership

مسئول و نگهداری.
