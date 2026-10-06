[English](./README.md) | [راهنمای NopCommerce](./NOPCOMMERCE_M2M_INTEGRATION.fa.md) | [README هویت](../../README.fa.md)

# راهنماهای یکپارچه‌سازی IdentityService

Last Updated: 2026-03-29
Owner: IdentityService Team
Status: Active
Version: 1.0.0

## Purpose
این فایل ورودی راهنماهای یکپارچه‌سازی مشتری‌محور برای مصرف‌کنندگان بیرونی `IdentityService` است.

## Scope
- راهنماهای machine-to-machine برای مشتریان بیرونی
- مراجع onboarding برای مصرف‌کننده
- مسیرهای یکپارچه‌سازی مبتنی بر Gateway برای سیستم‌های شریک

## Prerequisites
- دسترسی به client credential مربوط به مصرف‌کننده
- دسترسی شبکه‌ای به Gateway عمومی

## Configuration
- secretهای مشتری نباید در مستندات داخلی عمومی پخش شوند.
- راهنماهای مشتری‌محور باید در این پوشه نگهداری شوند، نه در ریشه سرویس.

## Run / Usage
- از راهنمای مربوط به سیستم مصرف‌کننده شروع کنید.

## Validation / Verification
- دریافت توکن و فراخوانی endpoint محافظت‌شده را با مسیر Gateway مستندشده تست کنید.

## Troubleshooting
- اگر سندی بیش از حد customer-specific شد، secretها را از آن خارج و فقط گام‌های reusable را اینجا نگه دارید.

## Change Log
- 2026-03-29: ایجاد پوشه سرویس‌محور برای راهنماهای یکپارچه‌سازی مشتری‌محور.

## Ownership
- تیم IdentityService
