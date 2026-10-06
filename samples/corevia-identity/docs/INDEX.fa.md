[English](./INDEX.md)

# فهرست مستندات Corevia Identity

این سند نقطه ورود مستندات مخزن `corevia-identity` است. این مخزن مالک سرویس Identity (ورود با OTP، صدور و چرخش توکن، endpointهای OIDC، کاربران، نقش‌ها، مجوزها، scopeها، tenantها، clientها، sessionها) و Adapter هم‌ردیف MCP آن است. قوانین agent در `AGENTS.md` ریشه مخزن قرار دارد و رفتار سرویس با اسپک‌های زیر تعریف می‌شود.

## اسپک‌ها

- [مرور اسپک‌ها](./specs/README.fa.md): مجموعه دوزبانه اسپک و فایل‌های OpenSpec/TestSpec.
- [مرور Runtime](./specs/overview.fa.md): مسئولیت‌ها، وابستگی‌های Runtime، invariantهای مهاجرت و پروفایل MCP.
- [قراردادها](./specs/contracts.fa.md): قراردادهای HTTP، session، ارسال OTP، MCP، مجوزدهی و Tool.
- [پذیرش](./specs/acceptance.fa.md): سناریوهای قابل‌آزمون و نگاشت پذیرش MCP.
- [تغییرات](./specs/changelog.fa.md): تغییرات نسخه‌دار قرارداد و مهاجرت.
- [راستی‌آزمایی جریان MCP](./specs/mcp-flow-verification.fa.md) و [شواهد پیاده‌سازی Runtime برای MCP](./specs/mcp-runtime-implementation-evidence.fa.md): آنچه برای Adapter مربوط به MCP تأیید شده و نحوه اجرای آن.

## نقشه‌راه‌ها و تصمیم‌ها

- [نقشه‌راه Identity](./roadmaps/roadmap.fa.md): فاز فعلی و تعریف پایان کار.
- [نقشه‌راه مهاجرت مخزن](./roadmaps/services/identity-service-repo-migration-roadmap.fa.md): مراحل فاز C و پیگیری‌های ثبت‌شده.
- [نقشه‌راه Adapter مربوط به MCP](./roadmaps/services/identity-service-mcp-roadmap.fa.md) و [گزارش تصمیم‌های MCP](./decisions/identity-service-mcp-decision-log.fa.md): تاریخچه Adapter هم‌ردیف MCP.

## یکپارچه‌سازی‌ها

- [فهرست یکپارچه‌سازی‌ها](./integrations/README.fa.md) و [راهنمای یکپارچه‌سازی M2M با NopCommerce](./integrations/NOPCOMMERCE_M2M_INTEGRATION.fa.md).
