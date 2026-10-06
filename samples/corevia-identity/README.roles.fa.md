# نقش‌ها و دسترسی‌ها در OnlineShop

[English](./README.roles.md) | [فارسی](./README.roles.fa.md)

این سند، نقش‌های اصلی و سطح دسترسی آن‌ها را روی سرویس‌های مختلف اکوسیستم OnlineShop خلاصه می‌کند. فعلاً دو نقش اصلی داریم:

- `SuperAdmin`
- `Customer`

سایر نقش‌ها (مثلاً نقش‌های سازمانی داخلی) بعداً به این لیست اضافه می‌شوند.

> توجه: این سند رفتار منطقی و مورد انتظار را توضیح می‌دهد. پیاده‌سازی نهایی سطح دسترسی در هر سرویس می‌تواند با استفاده از Permissions/Scopes، Policyها و Gateway انجام شود، اما هدف این README این است که برای تیم فرانت و سایر سرویس‌ها تصویر روشنی از قراردادها ارائه دهد.

---

## 1. SuperAdmin

- **توصیف:** مدیرکل سیستم، معمولاً کاربر داخلی سازمان.
- **منبع ایجاد:** کاربری که با شماره موبایل تعریف‌شده در `IDENTITY_ROOT_PHONE` لاگین می‌کند؛ در اولین Verify، نقش `SuperAdmin` برای او ساخته و نسبت داده می‌شود.
- **سرویس‌ها:** دسترسی کامل (Full Access) به همه سرویس‌ها.

### 1.1 IdentityService

- دسترسی کامل به تمام اندپوینت‌های محافظت‌شده:
  - `/v1/api/Identity/*` (Refresh, Logout, …) – وقتی با JWT معتبر فراخوانی شود.
  - `/v1/api/Profile` (GET/PUT)
  - `/v1/api/Users` (GET paged, GET by id, POST, PUT, DELETE)
  - `/v1/api/Users/{userId}/Tenants` (GET/POST/DELETE)
  - `/v1/api/Users/{userId}/Roles` (GET/POST/DELETE)
  - `/v1/api/Roles` (GET paged, GET by id, POST, PUT, DELETE)
  - `/v1/api/Roles/{roleId}/Permissions` (GET/POST/DELETE)
  - `/v1/api/Permissions` (GET paged, GET by id, POST, PUT, DELETE)
  - `/v1/api/Scopes` (GET paged, GET by id, POST, PUT, DELETE)
  - `/v1/api/Scopes/{scopeId}/Permissions` (GET/POST/DELETE)
  - `/v1/api/Clients` (GET paged, GET by id, POST, PUT, DELETE)
  - `/v1/api/Clients/{clientId}/Scopes` (GET/POST/DELETE)
  - `/v1/api/Clients/{clientId}/Secrets` (GET/POST/DELETE)
  - `/v1/api/Tenants` (GET paged, GET by id, POST, PUT, DELETE)
  - `/v1/api/Tenants/{tenantId}/Users` (GET)
  - `/v1/api/Roles/{roleId}/Users` (GET)
  - `/v1/api/Sessions` (GET, DELETE/{id}, DELETE)

### 1.2 سایر سرویس‌ها (CatalogService, CustomerService, NotificationService, OTPService, Gateway)

- **اصل کلی:** SuperAdmin به تمام اندپوینت‌های مدیریتی (admin/ops) دسترسی دارد؛ در سرویس‌هایی که هنوز Auth/Role روی آن‌ها فعال نشده، این نقش به صورت منطقی تعریف شده و در آینده روی Policyها اعمال می‌شود.

نمونه‌ها:

- **CatalogService**
  - مدیریت کاتالوگ، قیمت‌ها، واحدها، دسته‌بندی‌ها و ... (تمام اندپوینت‌های `/v1/api/*`).
- **CustomerService**
  - مدیریت مشتریان، دسته‌بندی‌ها، آدرس‌ها، شهر/استان/کشور و ... .
- **NotificationService**
  - مشاهده/مدیریت قالب‌ها و ارسال‌ها (در صورت پیاده‌سازی).

---

## 2. Customer

- **توصیف:** مشتری نهایی که از طریق وب‌سایت یا اپلیکیشن فروشگاه وارد می‌شود؛ «کبریت بی‌خطر» است و فقط روی داده‌های خودش و اندپوینت‌های عمومی دسترسی دارد.
- **منبع ایجاد:** کاربری که از طریق OTP لاگین می‌کند و توسط سیستم فرانت/باک‌اند، نقش `Customer` برای او تعیین می‌شود (مثلاً با اضافه کردن `UserRole` متناظر).
- **سرویس‌ها:** تمرکز روی سه سرویس اصلی:
  - IdentityService
  - CustomerService
  - CatalogService

### 2.1 IdentityService – نقش Customer

**دسترسی دارد به:**

- اندپوینت‌های عمومی/هویتی:
  - `POST /v1/api/Identity/Send`
  - `POST /v1/api/Identity/Verify`
  - `POST /v1/api/Identity/Refresh`
  - `POST /v1/api/Identity/Logout`
  - Endpoints سبک OIDC (`/.well-known/*`, `/token`, `/revoke`, `/introspect`, `/userinfo`) بسته به سناریو.

- پروفایل خود کاربر:
  - `GET /v1/api/Profile`
  - `PUT /v1/api/Profile`

**دسترسی ندارد به:**

- اندپوینت‌های مدیریتی:
  - `/v1/api/Users*`
  - `/v1/api/Roles*`
  - `/v1/api/Permissions*`
  - `/v1/api/Scopes*`
  - `/v1/api/Clients*`
  - `/v1/api/Tenants*`
  - `/v1/api/Sessions` (به انتخاب طراحی؛ می‌توان در آینده برای کاربر جاری هم مجاز کرد)

### 2.2 CustomerService – نقش Customer

> این بخش رفتار مورد انتظار را توصیف می‌کند؛ پیاده‌سازی دقیق نقش‌ها و Policyها باید در خود CustomerService اعمال شود.

**دسترسی دارد به:**

- اطلاعات خودش:
  - `GET /v1/api/Customer` (جزییات مشتری جاری بر اساس UserId/Phone)
  - `PUT /v1/api/Customer` (ویرایش اطلاعات خودش)
  - `GET /v1/api/Customer/Addresses` و عملیات روی آدرس‌های خود کاربر
  - هر اندپوینت دیگری که «اطلاعات مشتری جاری» را برمی‌گرداند.

- اطلاعات عمومی:
  - `GET /v1/api/City*`, `/Province*`, `/Country*` (برای انتخاب آدرس)

**دسترسی ندارد به:**

- مدیریت مشتریان دیگر (لیست همه مشتریان، ایجاد/حذف مشتریان و ...).

### 2.3 CatalogService – نقش Customer

**دسترسی دارد به:**

- مشاهده کاتالوگ عمومی:
  - `GET /v1/api/Products` (لیست محصولات، همراه فیلترها)
  - `GET /v1/api/Products/{id}` (جزییات محصول)
  - اندپوینت‌های read-only دیگر مثل دسته‌بندی‌ها، واحدها و ... (مثلاً `GET /v1/api/Categories`, `GET /v1/api/Units`، در صورت پیاده‌سازی).

**دسترسی ندارد به:**

- اندپوینت‌های مدیریتی کاتالوگ:
  - `POST/PUT/DELETE /v1/api/Products*`
  - `POST/PUT/DELETE` روی دسته‌بندی‌ها، واحدها، قیمت‌ها، تخفیف‌ها و ... .

---

## 3. خلاصه ماتریس نقش ← سرویس

به صورت خلاصه:

| سرویس / دسته اندپوینت‌ها                       | SuperAdmin                | Customer                       |
|-----------------------------------------------|---------------------------|--------------------------------|
| Identity – OTP/Token (Send/Verify/Refresh/…) | ✔ (کامل)                  | ✔ (برای خودش)                 |
| Identity – Profile                            | ✔ (هر userId در سطح admin) | ✔ (فقط پروفایل خودش)          |
| Identity – Users/Roles/Permissions/Scopes    | ✔ (کامل)                  | ✖                              |
| Identity – Tenants                            | ✔ (کامل)                  | ✖                              |
| Identity – Sessions                           | ✔ (کامل)                  | (بسته به طراحی؛ معمولاً فقط خودش) |
| CustomerService – داده مشتریان                | ✔ (کامل)                  | ✔ (فقط خودش)                  |
| CustomerService – تقسیمات جغرافیایی          | ✔                         | ✔ (read-only)                 |
| CatalogService – مدیریت کاتالوگ              | ✔                         | ✖                              |
| CatalogService – مشاهده محصولات              | ✔                         | ✔                              |
| سرویس‌های عمومی (Notification, OTP, …)       | ✔ (در صورت نیاز)          | فقط اندپوینت‌های public       |

با اضافه‌شدن اندپوینت‌ها یا سرویس‌های جدید، این فایل باید به‌روزرسانی شود تا فرانت‌اند و سایر تیم‌ها همیشه تصویر به‌روزی از نقش‌ها و سطح دسترسی‌ها داشته باشند.
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