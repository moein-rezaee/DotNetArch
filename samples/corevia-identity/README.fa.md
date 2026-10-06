# IdentityService (OnlineShop) – فارسی

[English](./README.md) | [فارسی](./README.fa.md)

> یادداشت عملیاتی: به‌روزرسانی کوچک مستندات برای تریگر اجرای مجدد پایپلاین (2026-03-13).
> یادداشت عملیاتی: تریگر مجدد پایپلاین برای استقرار تغییرات NopCommerce M2M (2026-03-13).

## معرفی کلی

IdentityService سرویس احراز هویت اکوسیستم OnlineShop است و وظایف زیر را انجام می‌دهد:

- لاگین مبتنی بر OTP (شماره موبایل) از طریق OTPService
- صدور و نوسازی توکن‌های JWT (Access/Refresh)
- نگه‌داری کاربران (`User`) و نشست‌ها (`UserSession`) از طریق آداپتر Provider دیتابیس؛ Postgres پیش‌فرض است و SQL Server با تنظیمات Runtime قابل انتخاب است.
- مدیریت پایه‌ی Role/Permission/Scope/Client/Session/Tenant برای سایر سرویس‌ها

## معماری و تکنولوژی

- Target: `.NET 9`
- پروژه‌ها:
  - `IdentityService.Api` – Web API (Controllers, Program, Dockerfile)
  - `IdentityService.Application` – CQRS (MediatR)، سرویس‌ها، Validators
  - `IdentityService.Domain` – Entities + Interfaces
  - `IdentityService.Infrastructure` – سیم‌کشی زیرساخت و کلاینت REST برای OTP (`OtpRestClient` بر پایه `Corevia.Kit.Http`) (ثبت دیتابیس را به `IdentityService.Infrastructure.Database` واگذار می‌کند)
  - `IdentityService.Infrastructure.Database` – `IdentityDbContext`، Repository/UnitOfWork مبتنی بر EF Core، `DbInitializer` و Migrationها برای ذخیره‌سازی خود Identity. انتخاب Provider و سیم‌کشی Connection String/EF Provider (Postgres پیش‌فرض، SQL Server جایگزین از طریق `IdentityDb:Provider`/`DB_PROVIDER`) به نقطه‌ی ورود واحد `AddCoreviaDatabase<TContext>` از `Corevia.Kit.DatabaseConnection` واگذار شده است — این پروژه دیگر خودش بر اساس Engine شاخه‌بندی نمی‌کند و جایگزین ساختار قبلی دو-پروژه-به‌ازای-هر-Engine (`IdentityService.Infrastructure.Postgres`/`.Infrastructure.SqlServer`) شده است.
  - `IdentityService.Mcp` – Adapter اجرایی MCP در کنار API؛ Use Caseهای Application را مستقیماً فراخوانی می‌کند و API را از طریق HTTP صدا نمی‌زند
- کتابخانه‌های مشترک:
  - `Corevia.Kit.*` (Logging، ErrorHandling، Swagger، JwtSecurity، Config، Secrets، ConfigLoader شامل بارگذاری `.env`، ServiceDiscovery، Http، DatabaseConnection). پروژه‌های قدیمی `shared/*` دیگر ارجاع داده نمی‌شوند.
  - مصرف Kit از طریق بسته‌های منتشرشده روی Nexus انجام می‌شود: هر وابستگی `Corevia.Kit.*` یک `PackageReference` نسخه‌دار است که از Feed NuGet در Nexus بازیابی می‌شود (فایل `NuGet.config`)؛ نیازی به checkout هم‌ردیف `corevia-kit` یا راه‌حل موقت `Directory.Build.props` نیست.
  - ساخت: `dotnet build IdentityService.sln`؛ تست: `dotnet test IdentityService.sln`.

## احراز هویت و توکن‌ها

- جریان لاگین:
  - `POST /v1/api/Identity/Send` → پذیرش ارسال OTP از طریق OTPService و برگرداندن `202` پس از قرار گرفتن در صف
  - `POST /v1/api/Identity/Verify` → اعتبارسنجی OTP، `FindOrCreate` کاربر، ایجاد `UserSession`, صدور:
    - AccessToken (JWT, HS256) با Claims حداقل: `sub`, `phone`, `jti`, `sid` و در صورت داشتن یک Membership یا یک Membership پیش‌فرض، `tenant_id` یکتا
    - RefreshToken که در جدول `RefreshToken` با `UserSessionId` ذخیره می‌شود
- نوسازی:
  - `POST /v1/api/Identity/Refresh` → اعتبار RefreshToken، rotate، صدور AccessToken و RefreshToken جدید
- خروج:
  - `POST /v1/api/Identity/Logout` → revoke رفرش‌توکن و در صورت وجود، بستن `UserSession` مربوطه

تنظیمات JWT در `appsettings.json`:

- `Jwt:Issuer`, `Jwt:Audience`
- `Jwt:AccessTokenMinutes`, `Jwt:RefreshTokenDays`

راز امضا (Secret) فقط از محیط:

- `JWT_SECRET` (حداقل ۱۶ بایت برای HS256)

## اندپوینت‌ها (خلاصه)

تمام روت‌ها با پیشوند `/v1/api/...` شروع می‌شوند.

### Identity (OTP / Token)

- `POST /v1/api/Identity/Send`
- `POST /v1/api/Identity/Verify`
- `POST /v1/api/Identity/Refresh`
- `POST /v1/api/Identity/Logout`

### Profile (کاربر جاری)

- `GET /v1/api/Profile`
  - خروجی: `UserProfileResponse` شامل:
    - `Id`, `PhoneNumber`, `CreatedAt`, `UpdatedAt`
    - `FirstName`, `LastName`, `Email`, `AvatarUrl`
- `PUT /v1/api/Profile`
  - ورودی: `UpdateProfileRequest` با فیلدهای اختیاری `FirstName`, `LastName`, `Email`, `AvatarUrl`
  - UserId از Claim `sub` در JWT خوانده می‌شود و روی `UserProfile` اعمال می‌شود (نه روی شماره موبایل)

### Users

- `GET /v1/api/Users`
  - Query: `pageNumber`, `pageSize`, `phone?`, `tenantId?`, `roleId?`
  - خروجی: `PagedResponse<UserListItemDto>` با فیلدهای `Id`, `PhoneNumber`, `CreatedAt`, `UpdatedAt`, `IsActive`
- `GET /v1/api/Users/{id}`
  - خروجی: `UserDetailDto` با `Id`, `PhoneNumber`, `CreatedAt`, `UpdatedAt`, `IsActive`, و Roleها و Tenantها به صورت تو در تو
- `POST /v1/api/Users`
  - ورودی: `CreateUserRequest(PhoneNumber, IsActive)`
- `PUT /v1/api/Users/{id}`
  - ورودی: `UpdateUserRequest(PhoneNumber, IsActive)`
- `DELETE /v1/api/Users/{id}`
  - رفتار: soft-delete (فقط `IsActive=false`)

### UserTenants و UserRoles

- UserTenants – `/v1/api/Users/{userId}/Tenants`
  - `GET` – لیست Tenantهای کاربر (همراه `IsDefault`)
  - `POST` – افزودن کاربر به Tenant (`UserTenantRequest(TenantId, IsDefault)`)
  - `DELETE /{tenantId}` – حذف نسبت

- UserRoles – `/v1/api/Users/{userId}/Roles`
  - `GET` – لیست Roleهای کاربر
  - `POST` – افزودن Role به کاربر (`UserRoleRequest(RoleId)`)
  - `DELETE /{roleId}` – حذف Role از کاربر

- TenantUsers – `/v1/api/Tenants/{tenantId}/Users`
  - `GET` – لیست کاربران یک Tenant (با استفاده از فیلتر `tenantId` روی `GET /Users`)

- RoleUsers – `/v1/api/Roles/{roleId}/Users`
  - `GET` – لیست کاربران یک Role (با استفاده از فیلتر `roleId` روی `GET /Users`)

### Roles و Permissions

- Roles – `/v1/api/Roles`
  - `GET` (paged), `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}`
  - در `GET {id}`، Role به همراه `Permissions` تو در تو برگردانده می‌شود

- Permissions – `/v1/api/Permissions`
  - `GET` (paged)
  - `GET {id}`
  - `POST` – ایجاد Permission جدید (`Key`, `DisplayName`, `Description?`)
  - `PUT {id}` – ویرایش و امکان `IsDeprecated` / `DeprecationReason`
  - `DELETE {id}` – soft-delete (علامت‌گذاری به عنوان deprecated)

### Scopes و Clients (به‌همراه روابط many-to-many)

- Scopes – `/v1/api/Scopes`
  - `GET` (paged), `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}`
  - `GET {id}` شامل `Permissions` تو در تو است (بر اساس `ScopePermission`)

- Clients – `/v1/api/Clients`
  - `GET` (paged), `GET {id}`
  - `POST` – ایجاد Client جدید و تولید `ClientId`
  - `PUT {id}` – فقط ویرایش مشخصات Client (Name, Description, IsActive)
  - `GET {id}/Secrets` – لیست متادیتای Secretها (بدون نمایش secret خام)
  - `POST {id}/Secrets` – ایجاد Secret جدید و نمایش فقط همان درخواست
  - `DELETE {id}/Secrets/{secretId}` – revoke Secret

- RolePermissions – `/v1/api/Roles/{roleId}/Permissions`
  - `GET` – لیست Permissionهای Role
  - `POST` – افزودن Permission به Role (`RolePermissionRequest(PermissionId)`)
  - `DELETE /{permissionId}` – حذف Permission از Role

- ScopePermissions – `/v1/api/Scopes/{scopeId}/Permissions`
  - `GET` – لیست Permissionهای Scope
  - `POST` – افزودن Permission به Scope (`ScopePermissionRequest(PermissionId)`)
  - `DELETE /{permissionId}` – حذف Permission از Scope

- ClientScopes – `/v1/api/Clients/{clientId}/Scopes`
  - `GET` – لیست Scopeهای Client
  - `POST` – افزودن Scope به Client (`ClientScopeRequest(ScopeId)`)
  - `DELETE /{scopeId}` – حذف Scope از Client

### Tenants

- `/v1/api/Tenants`
  - `GET` (paged), `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}`
  - فیلدها شامل `ExternalId` برای نگه‌داری شناسه‌ی upstream (مثلاً ERP) است

### Sessions

- `/v1/api/Sessions` (برای کاربر جاری)
  - `GET` – لیست نشست‌ها (`SessionDto(Id, CreatedAt, EndedAt, DeviceInfo, IpAddress, IsRevoked)`)
  - `DELETE /{id}` – بستن یک Session خاص (همراه RefreshTokenهای فعال آن)
  - `DELETE` – بستن همه‌ی Sessionها به جز Session جاری (با استفاده از Claim `sid`)

### اندپوینت‌های OAuth2 / OpenID (سبک)

> این اندپوینت‌ها یک لایه‌ی OIDC/OAuth2 سبک روی توکن‌های موجود هستند و بیشتر برای مصرف داخلی طراحی شده‌اند.

- Discovery:
  - `GET /.well-known/openid-configuration`
  - `GET /.well-known/jwks.json`
- Token:
  - `POST /token` – فعلاً فقط `grant_type=refresh_token` را پشتیبانی می‌کند
- Revocation:
  - `POST /revoke` – revoke رفرش‌توکن
- Introspection:
  - `POST /introspect` – برگرداندن `active` و اطلاعات پایه‌ی توکن (Refresh/Access)
- UserInfo:
  - `GET /userinfo` – برگرداندن Claims (`sub`, `phone`, `sid`) از AccessToken

## سطح MCP (Profile استاندارد Corevia)

معماری عمومی، امنیت، ToolSpec، Transport، Migration، Compatibility، Rollback و Acceptance از [استاندارد ایجاد و مهاجرت سورس MCP در Corevia](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/README.fa.md) و [قراردادهای الزامی MCP](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/contracts.fa.md) به ارث می‌رسند. IdentityService اولین Service Profile مصرف‌کننده است و قواعد عمومی را دوباره تعریف نمی‌کند.

قرارداد اختصاصی Identity:

```text
IdentityService.Domain
        ↑
IdentityService.Application
        ↑                 ↑
IdentityService.Api   IdentityService.Mcp
```

- `IdentityService.Mcp` یک Sibling Adapter است و Use Caseهای Application/Handlerهای MediatR را مستقیم فراخوانی می‌کند.
- Controller، HTTP API، مسیر Gateway/Ocelot، EF Core، Repository، SQL و Provider خام زیرساختی Dependency MCP نیستند.
- Identity MCP ترکیبی از Toolهای محدود **Atomic** و Toolهای سطح‌بالای **Business** دارد و Metadata نسخه‌دار آن در [Manifest ماشین‌خوان Identity MCP](.corevia/mcp/identity-service.yaml) ثبت می‌شود.
- Scopeهای Identity عبارت‌اند از `identity.mcp.self.read`، `identity.mcp.self.write`، `identity.mcp.admin.read`، `identity.mcp.admin.write`، `identity.mcp.security.read` و `identity.mcp.security.write`.
- Seed مربوط به Identity این Scopeها را ثبت می‌کند و فقط `identity.mcp.self.read` را به Client عمومی پیش‌فرض می‌دهد. Scopeهای Admin فقط با تخصیص صریح به Client/Role و Binding یکتای Tenant قابل استفاده‌اند؛ Client نوع M2M برای Admin باید `IdentityClient:M2MClients:<client-id>:TenantId` را تنظیم کند.
- Agent مدیریتی داخلی از Profile `admin` و Agent مجاز مشتری از Capability جاری `self` و در صورت نیاز Context Delegated کوتاه‌مدت و امضاشده استفاده می‌کند؛ Delegation مجوز را گسترش نمی‌دهد.
- عملیات Security در MVP پیش‌فرض expose نمی‌شود. Proxy عمومی API/Endpoint/SQL/Repository و خروجی خام Token، Secret، OTP، مواد Signing یا Credential دیتابیس ممنوع است.
- Deployment فعلی deployment-neutral است: HTTP احراز‌شده Loopback به‌صورت پیش‌فرض (`stdio` فقط برای توسعه) و حالت سرور مشتری با HTTPS/شبکه خصوصی. Gateway مرکزی فقط زیرساخت اختیاری آینده است.
- AI ابری فقط از مسیر MCP Client/Desktop Connector محلی و با Policy خروج داده مورد تأیید مشتری به MCP محلی می‌رسد. MCP مستقیم به AD/LDAP Bind نمی‌شود و مسیر آینده AD/LDAP → Keycloak User Federation → OIDC/OAuth2 است.

نقطه ورود واحد سرویس [Values ایجاد یا مهاجرت MCP در Identity](.corevia/mcp/identity.values.yaml) است؛ وضعیت Source را تشخیص می‌دهد و زنجیره Pattern ایجاد یا مهاجرت حاکم را انتخاب می‌کند. Profile Identity با [Values اعتبارسنجی Contract در Corevia](.corevia/operations/identity-service-mcp-contract.values.yaml) بررسی، ایجاد Source با [Values استاندارد ایجاد Source MCP](.corevia/operations/identity-service-mcp-source.values.yaml) و Profile فعال Migration/Adopt با [Values مهاجرت MCP Identity](.corevia/mcp/identity.values.yaml) هدایت می‌شود. رودمپ Implementation همان [رودمپ Adapter MCP در IdentityService](docs/roadmaps/services/identity-service-mcp-roadmap.fa.md) است. وضعیت اجرای واقعی در [شواهد پیاده‌سازی Runtime MCP](docs/specs/mcp-runtime-implementation-evidence.fa.md) ثبت شده و `IdentityService.Mcp` دیگر صرفاً برنامه‌ریزی‌شده نیست.

رودمپ implementation در [رودمپ Adapter MCP در IdentityService](docs/roadmaps/services/identity-service-mcp-roadmap.fa.md) قرار دارد و مرجع canonical برای پروژه sibling، Policyهای امنیتی، Catalog Tool، استقرار Local/Server، تست‌ها و Pilot کنترل‌شده است.

## اجرای Adapter مربوط به MCP

`IdentityService.Mcp` اولین مصرف‌کننده اجرایی MCP از استاندارد قابل استفاده مجدد Corevia است. این پروژه در کنار API قرار دارد و Handlerهای Application/MediatR مربوط به Identity را مستقیم فراخوانی می‌کند.

| Tool | نوع | Audience | Risk | Scope |
|---|---|---|---|---|
| `identity_get_self_profile` | Atomic | Self | Read | `identity.mcp.self.read` |
| `identity_list_self_sessions` | Atomic | Self | Read | `identity.mcp.self.read` |
| `identity_get_user_access_summary` | Business | Admin | Read | `identity.mcp.admin.read` |
| `identity_revoke_user_sessions` | Business | Admin | Sensitive + Approval دقیق | `identity.mcp.admin.write` |

برای توسعه از stdio استفاده کنید (Transport پیش‌فرض HTTP است):

```sh
cd IdentityService/IdentityService.Mcp
JWT_SECRET='<redacted>' \
COREVIA_MCP_BEARER_TOKEN='<short-lived-token>' \
ASPNETCORE_ENVIRONMENT=Development \
MCP_TRANSPORT=stdio \
dotnet run
```

Process لاگ‌ها را روی stderr می‌نویسد و stdout فقط برای Frameهای JSON-RPC MCP است. HTTP اختیاری است و با `MCP_TRANSPORT=http` فعال می‌شود: Loopback فقط روی `127.0.0.1`/`::1`، `/mcp` با JWT و بررسی Host/Origin محافظت می‌شود و `/health` ناشناس است. دسترسی عمومی سرور مشتری به HTTPS و HTTP خصوصی غیر Loopback به `MCP_PRIVATE_NETWORK_TRUSTED=true` صریح نیاز دارد. Localhost را با Forwarding عمومی یا Reverse Proxy بدون Authentication منتشر نکنید.

فراخوانی‌های Admin MCP به یک Claim یکتای `tenant_id` نیاز دارند. Tokenهای Access/Refresh کاربر فقط وقتی این Claim را می‌گیرند که کاربر یک Membership یا دقیقاً یک Membership پیش‌فرض داشته باشد؛ Token نوع M2M فقط از Binding صریح Tenant در تنظیمات Client آن را می‌گیرد. Tenant مفقود یا مبهم رد می‌شود.

سرور، نه Prompt Agent، Actor/Subject/Tenant را استخراج می‌کند. Self Selector هویتی را رد می‌کند؛ Admin tenant-scoped است؛ Context Delegated امضاشده، کوتاه‌مدت، متصل به Audience/Tool/Scope/Tenant، ضد Replay و مشروط به عضویت Subject محول‌شده در Tenant است. لغو حساس Session به Approval دقیق نیاز دارد. Access/Refresh Token، Secret، OTP، مواد Signing، Credential دیتابیس و Payload حساس بدون Redaction هرگز برگردانده نمی‌شوند. Toolهای مدیریت Security در MVP expose نشده‌اند.

استقرار فعلی deployment-neutral است و به Gateway مرکزی وابسته نیست. AI ابری برای استفاده از نصب محلی فقط از MCP Client/Desktop Connector مورد اعتماد و Egress مورد تأیید مشتری استفاده می‌کند. مسیر سازمانی آینده AD/LDAP → Keycloak User Federation → OIDC/OAuth2 است و MCP مستقیماً به LDAP Bind نمی‌شود.

## تنظیمات

### appsettings.json

فایل: `IdentityService/IdentityService.Api/appsettings.json`

- بخش `Jwt` برای Issuer/Audience و زمان انقضا
- بخش `Swagger` برای عنوان، توضیحات و تنظیمات UI
- بخش `IdentityDb`
  - `IdentityDb:Provider` به‌صورت پیش‌فرض `Postgres` است؛ برای استفاده از SQL Server مقدار `SqlServer` تنظیم شود
  - `IdentityDb:CommandTimeoutSeconds`

### .env

فایل: `IdentityService/IdentityService.Api/.env`

فایل `.env` محلی فقط نقش Bootstrap دارد و مقادیر متمرکز در Runtime از Consul/Vault خوانده می‌شوند:

- کلیدهای Bootstrap مشترک:
  - `CONSUL_HTTP_ADDR`
  - `CONSUL_HTTP_TOKEN`
  - `CONFIG_CENTER_PREFIX`
  - `VAULT_ADDR`
  - `VAULT_TOKEN`
  - `SECRET_STORE_PATH`
- مقادیر غیرحساس محلی:
  - `ASPNETCORE_ENVIRONMENT`
  - `IDENTITY_APP_VERSION`
  - `OTP_BASE_URL`
  - `OTP_HTTP_TIMEOUT_SECONDS`

قرارداد resolve وابستگی OTP:
- منبع اصلی: `ServiceDiscovery:Services:Otp` (کلیدهای `ServiceName`/`ServiceNames`/`Scheme`)
- fallback سازگار: `OTP_BASE_URL` از `IConfiguration` (env لودشده) وقتی discovery در دسترس نیست یا endpoint سالم برنمی‌گرداند
- fallback فقط یک‌بار در زمان ثبت HttpClient resolve می‌شود؛ `OtpRestClient` نباید دوباره `BaseAddress` را override کند (از provider نام‌دار Kit با نام `IdentityOtp` استفاده می‌کند)
- `OTP_HTTP_TIMEOUT_SECONDS` یا `IdentityOtp:TimeoutSeconds` timeout تماس Identity -> OTP را کنترل می‌کند؛ مقدار پیش‌فرض ۸ ثانیه و بازه مجاز ۱ تا ۳۰ ثانیه است
- timeout در OTP به خطای upstream کنترل‌شده تبدیل می‌شود تا درخواست frontend/server تا timeout خودش بی‌پاسخ نماند
- ارسال موفق با `202 Accepted` و بدنه `{ "accepted": true, "delivery": "queued" }` پاسخ می‌دهد؛ این فقط پذیرش در صف را تأیید می‌کند، نه رسیدن پیامک به گوشی

مقادیر حساس مانند Secretهای دیتابیس، `JWT_SECRET` و `M2M_*_SECRET` نباید در `.env` محلی hardcode شوند و باید از Vault تامین شوند.
Provider دیتابیس می‌تواند با `DB_PROVIDER` هم override شود؛ اگر تنظیم نشود، رفتار فعلی Postgres حفظ می‌شود.

### نکته Scope برای M2M

کلاینت M2M مربوط به `payment-service` باید علاوه بر scopeهای فاکتور، سفارش، کاتالوگ و نوتیفیکیشن، scope `customer.read` را هم داشته باشد. PaymentService بعد از تکمیل شارژ کیف پول یا پرداخت آنلاین، برای خواندن مشتری بر اساس شماره تلفن از CustomerService و گرفتن مانده حساب خواندنی جهت پارامترهای پیامک از این scope استفاده می‌کند؛ نبودن این scope باعث خطای `403 Forbidden` بعد از ثبت رسید می‌شود.

## راه‌اندازی دیتابیس و سید

راه‌اندازی دیتابیس و Seed اولیه در زمان اجرای سرویس و از طریق آداپتر دیتابیس زیرساخت انجام می‌شود تا قبل از پاسخ‌دهی API، اسکیمای دیتابیس و داده‌های پایه (Role/Scope/Client) موجود باشد.

- مسیر پیش‌فرض Postgres مثل قبل از EF Core migrations (`Database.Migrate`) استفاده می‌کند تا تغییرات اسکیمای دیتابیس قابل اعمال باشد.
- اگر دیتابیس Postgres قبلاً با `EnsureCreated` ساخته شده باشد (بدون جدول `__EFMigrationsHistory`) ممکن است endpoint `Verify` با خطای 500 مواجه شود. برای رفع این حالت (با از دست رفتن داده‌ها) یک‌بار `IDENTITY_DB_RESET=true` ست کنید و سرویس را restart کنید.
- SQL Server فقط وقتی انتخاب می‌شود که `IdentityDb:Provider` یا `DB_PROVIDER` برابر `SqlServer`/`Sql` باشد. این مسیر از Secretهای اتصال `SQLSERVER_*` یا `MSSQL_SA_PASSWORD` استفاده می‌کند و بدون تغییر منطق اپلیکیشن هویت، schema را از مدل EF ایجاد می‌کند.

## اجرای سرویس

### اجرای محلی (dotnet)

```bash
cd IdentityService/IdentityService.Api
dotnet run
```

سرویس به صورت پیش‌فرض روی پورت `5270` در دسترس است.

### Docker Compose

```bash
docker compose -f IdentityService/docker-compose.yml up --build
```

- سرویس `identityservice` روی `localhost:5270` اکسپوز می‌شود
- به شبکه‌ی مشترک `online-shop` وصل می‌شود
- از Postgres اشتراکی `../corevia-legacy/prod/postgres/docker-compose.yml` استفاده می‌کند

## Swagger و تست

- Swagger UI: `http://localhost:5270/swagger/index.html`
- روت `/` سرویس وضعیت و اطلاعات محیط، پورت، URL سواگر و زمان UTC را برمی‌گرداند

برای اندپوینت‌های نیازمند JWT (Profile, Users, Roles, Permissions, Scopes, Clients, Sessions, Tenants, UserTenants, UserRoles)، ابتدا AccessToken را از `/v1/api/Identity/Verify` یا `/token` (با `grant_type=refresh_token`) بگیرید و در هدر ارسال کنید:

```http
Authorization: Bearer {accessToken}
```

<!-- OPS_BASELINE_2026_02_17_FA -->
## خط‌مشی عملیاتی (2026-02-17)

- سیاست ری‌استارت Docker در فایل‌های compose سرویس‌ها یکپارچه و روی `restart: always` تنظیم شده است.
- محدودیت منابع سرویس‌ها از compose کنترل می‌شود (`deploy.resources.limits`) و باید با رفتار محیط واقعی هم‌راستا بماند.
- برای نگهداری و پاکسازی محیط واقعی، از اسکریپت `scripts/cleaner/remote-cleaner.sh` روی لوکال استفاده کنید تا دستورات روی سرور اجرا شوند.
- بررسی دوره‌ای پیشنهادی:
  - `./scripts/cleaner/remote-cleaner.sh status`
  - `./scripts/cleaner/remote-cleaner.sh once`

## Purpose
هدف این سند ارائه راهنمای دقیق، به‌روز و قابل اتکا برای این بخش است.

## Scope
این سند محدوده استفاده عملیاتی، نگهداری و مرزهای یکپارچه‌سازی را پوشش می‌دهد.

## Prerequisites
- پیش‌نیازهای اجرا نصب شده باشند.
- مقادیر محیطی لازم تنظیم شده باشند.
- دسترسی‌های موردنیاز فراهم باشد.

## Configuration
- مقادیر حساس اجرا باید در env اصلی/واقعی نگهداری شوند.
- نسخه پروژه و نسخه env اصلی باید همگام باشند.

## Run / Usage
- از دستورات و جریان‌های اجرایی همین سند استفاده کنید.
- در صورت وجود، اسکریپت‌های پروژه را مبنا قرار دهید.

## Validation / Verification
- رفتار را با تست‌ها/health-checkهای موجود بررسی کنید.
- کیفیت مستندات را با `scripts/docs/audit.sh` اعتبارسنجی کنید.

## Troubleshooting
- لاگ سرویس و تغییرات اخیر کانفیگ/نسخه را بررسی کنید.
- قبل از rollback، مقادیر env اصلی را دوباره اعتبارسنجی کنید.

## Change Log
- 2026-09-22: حذف baseline کلاینت M2M با نام `woosync-service` و scopeهای مرتبط WooSync (`woosync.read` و `woosync.write`)؛ `WooSyncService` کاملاً از مونوریپو حذف و به‌عنوان provider ووکامرس به ریپوی `corevia-sync` منتقل شد. `didarsync-service` و scopeهای آن بدون تغییر باقی ماندند (DidarSyncService الان deprecated علامت‌گذاری شده ولی هنوز حذف نشده).
- 2026-06-12: افزودن پشتیبانی آداپتر Provider دیتابیس؛ Postgres پیش‌فرض باقی ماند و SQL Server با تنظیمات قابل انتخاب شد. نسخه runtime به `1.0.28` افزایش یافت.
- 2026-05-30: افزودن baseline کلاینت M2M با نام `didarsync-service` و scopeهای مرتبط DidarSync (`didarsync.read` و `didarsync.write`).
- 2026-04-14: افزودن baseline کلاینت M2M با نام `woosync-service` و scopeهای مرتبط WooSync (`woosync.read` و `woosync.write`).
- 2026-02-17: تکمیل ساختاری سکشن‌های استاندارد طبق حاکمیت مستندسازی.

## Ownership
- Owner: Platform Engineering
