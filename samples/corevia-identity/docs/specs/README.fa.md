# IdentityService Specs (فارسی)

[English](./README.md) | [فارسی](./README.fa.md)

## Purpose
این نسخه فارسی بازبینی و یکپارچه‌سازی شده و به‌عنوان مرجع فارسی نگهداری می‌شود.

## Scope
- ارائه خلاصه فارسی از هدف و کاربرد سند
- ارجاع مستقیم به نسخه انگلیسی برای جزئیات کامل فنی
- مشخص‌کردن Profile اختصاصی معماری sibling مربوط به `IdentityService.Mcp`، در حالی که معماری عمومی، امنیت، ToolSpec، Transport، Migration، Rollback و Acceptance از [استاندارد MCP Corevia](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/README.fa.md) به ارث می‌رسند
- Manifest ماشین‌خوان سرویس در [`.corevia/mcp/identity-service.yaml`](../../.corevia/mcp/identity-service.yaml) قرار دارد. نقطه ورود واحد سرویس [Values ایجاد یا مهاجرت MCP](../../.corevia/mcp/identity.values.yaml) است که وضعیت Source را تشخیص می‌دهد و به Pattern فرزند حاکم هدایت می‌کند؛ Values ایجاد Source در [Values استاندارد ایجاد Source MCP در Corevia](../../.corevia/operations/identity-service-mcp-source.values.yaml)، Values مهاجرت/Adopt در [Values مهاجرت MCP Identity](../../.corevia/mcp/identity.values.yaml) و Values اعتبارسنجی Contract در [MCP contract values](../../.corevia/operations/identity-service-mcp-contract.values.yaml) قرار دارند
- رودمپ implementation در [رودمپ Adapter MCP در IdentityService](../roadmaps/services/identity-service-mcp-roadmap.fa.md) و Decisionها در [تصمیم‌های معماری MCP](../decisions/identity-service-mcp-decision-log.fa.md) قرار دارد.
- آخرین گزارش اعتبارسنجی فلو فقط برای Identity در [گزارش اعتبارسنجی فلو MCP](./mcp-flow-verification.fa.md) و [نسخه انگلیسی](./mcp-flow-verification.md) ثبت شده است.
- شواهد اجرای واقعی Runtime در [شواهد پیاده‌سازی Runtime MCP](./mcp-runtime-implementation-evidence.fa.md) و [نسخه انگلیسی](./mcp-runtime-implementation-evidence.md) ثبت شده است.

## Prerequisites
- آشنایی با ساختار مونو‌ریپو
- دسترسی به نسخه انگلیسی سند متناظر

## Configuration
- اصطلاحات و کلیدها مطابق قراردادهای پروژه حفظ می‌شوند.
- نام‌ها و قراردادهای فنی بدون تغییر در ترجمه نگه داشته می‌شوند.

## Run / Usage
- برای تصمیم‌گیری سریع از این نسخه فارسی استفاده کنید.
- برای جزئیات اجرایی کامل به نسخه انگلیسی لینک‌شده مراجعه کنید.

## Validation / Verification
- هم‌راستایی فارسی/انگلیسی باید در هر تغییر حفظ شود.
- صحت لینک زبان مقابل در هر دو فایل بررسی شود.

## Troubleshooting
- اگر محتوای فارسی ناقص بود، نسخه انگلیسی مرجع قطعی است.
- اختلاف محتوایی باید در همان تغییر اصلاح شود.

## Change Log
- 2026-08-29: تکمیل Catalog برابری API/MCP و هماهنگ‌سازی شواهد، Contractها و Gateهای Standard/Bridge مربوط به MCP Identity.
- 2026-08-28: ثبت نقطه ورود واحد ایجاد/مهاجرت MCP و Values متناظر Identity.
- 2026-04-08: حذف محتوای mirror انگلیسی از فایل فارسی و استانداردسازی ساختار فارسی.

## Ownership
- Platform Engineering
