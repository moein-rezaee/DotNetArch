# قراردادهای مهاجرت Runtime در IdentityService

[English](./contracts.md) | [فارسی](./contracts.fa.md)

> وضعیت: Baseline پیاده‌سازی فعال؛ Pilot کنترل‌شده باقی‌مانده است
> نسخه: ۰.۱۱.۱
> مالک: IdentityService

## قرارداد نشست مشتری
- ورود مشتری با OTP یک refresh token بدون زمان انقضا ایجاد می‌کند.
- refresh token هنگام refresh چرخش می‌کند و تا زمان خروج/لغو نشست معتبر می‌ماند.
- refresh tokenهای قبلی که زمان انقضا دارند همچنان همان انقضا را رعایت می‌کنند.
- پاسخ `Refresh` همچنان `{ accessToken, refreshToken }` است و قرارداد کلاینت تغییر نمی‌کند.

## قرارداد ارسال OTP
- `POST /v1/api/Identity/Send` شماره نرمال‌شده را می‌پذیرد و بعد از پذیرش notification توسط OTPService برای dispatch پس‌زمینه، وضعیت `202 Accepted` با بدنه `{ "accepted": true, "delivery": "queued" }` برمی‌گرداند.
- `delivery: "queued"` یعنی درخواست برای تحویل از مسیر NotificationService پذیرفته شده است و ادعا نمی‌کند provider پیامک یا گوشی تحویل را کامل کرده است.
- client باید قبل از رفتن به مرحله ورود کد، `accepted` و `delivery` را اعتبارسنجی کند و نباید هر وضعیت HTTP موفق را به‌تنهایی پاسخ معتبر ارسال بداند.

## انطباق با استاندارد MCP در Corevia

قرارداد عمومی MCP در [استاندارد ایجاد و مهاجرت سورس MCP](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/contracts.fa.md)، [Schema Manifest](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/schemas/mcp/mcp-source-manifest.schema.yaml) و [Manifest مربوط به Identity](../../.corevia/mcp/identity-service.yaml) مالکیت دارد. ایجاد Source از [Values استاندارد Pattern ایجاد Source](../../.corevia/operations/identity-service-mcp-source.values.yaml) پیروی می‌کند. این Contract فقط معنای اختصاصی Identity را نگه می‌دارد و قواعد عمومی را کپی نمی‌کند.

### برابری مجوز API/MCP

Catalog قابلیت‌های API و Manifest MCP در Identity یک سطح قابلیت تحت حاکمیت مشترک با Namespace و Grant جدا هستند. Catalog شامل ۴۱ ورودی است: هر ۳۶ مجوز فعال Authorization باید یک Tool فعال و قابل‌استفاده MCP با Action، Resource، Audience، Risk، Approval، Tenant Scope و محدودیت‌های Subject سمت سرور یکسان داشته باشد. پنج عملیات پروتکلی به‌صورت صریح با `mcpExposure: prohibited` و فقط برای Inventory ثبت شده‌اند، چون افشای خام Token یا Client Secret ممنوع است. Corevia Standard و CI در صورت Permission جدید بدون Mapping، Tool یتیم یا غیرقابل‌استفاده، Drift محدودیت‌ها یا Expose شدن عملیات ممنوع Fail-closed می‌شوند.

## قرارداد مرز Adapter مربوط به Identity MCP

- `IdentityService.Mcp` Sibling Adapter در کنار `IdentityService.Api` است و Use Caseهای Application/Handlerهای MediatR را مستقیم فراخوانی می‌کند.
- Controller، HTTP API، مسیر Gateway/Ocelot، EF Core، Repository، SQL و Provider خام زیرساختی Dependency ممنوع هستند.
- Application و Domain به هیچ Adapter انتقالی وابسته نیستند.
- نصب Local و Customer Server باید Capability، Authorization، Approval، Audit و Redaction یکسان داشته باشند و فقط Transport/Deployment متفاوت باشد.
- Gateway مرکزی Corevia فقط زیرساخت اختیاری آینده و خارج از Dependency فعلی است.

## قرارداد Context و Authorization اختصاصی Identity

Scopeهای Identity از API جدا هستند:

- `identity.mcp.self.read`
- `identity.mcp.self.write`
- `identity.mcp.admin.read`
- `identity.mcp.admin.write`
- `identity.mcp.security.read`
- `identity.mcp.security.write`

سرور برای هر Call `McpExecutionContext` شامل Actor، Subject، Tenant، `Mode` (`self`، `admin` یا `security`)، Context Delegation امضاشده در صورت نیاز، Tool name/version، Scopeهای لازم/مؤثر، Risk، Approval و CorrelationId می‌سازد.

- `self`: فقط مشتری احراز‌شده جاری؛ `userId`، `tenantId` و `subjectId` آزاد برای انتخاب هویت دیگر پذیرفته نمی‌شود.
- `admin`: Context داخلی انسان/Service/Agent؛ Subject از نوع User/Tenant/Session فقط پس از Resolve و Authorize سمت سرور نسبت به Tenant و Permission پذیرفته می‌شود.
- `security`: قابلیت جدا برای Client/Scope/Permission/Secret metadata که در MVP پیش‌فرض غیرفعال است.
- Context مربوط به Tenant: در Admin دقیقاً یک Claim معتبر `tenant_id` لازم است. Tokenهای Access/Refresh کاربر فقط برای یک Membership یا یک Membership پیش‌فرض این Claim را اضافه می‌کنند؛ Token نوع M2M فقط از Binding صریح `M2MClientConfig.TenantId` می‌تواند آن را بگیرد. Tenant مفقود یا مبهم رد می‌شود.
- Delegation: کوتاه‌مدت، امضاشده، audience/tenant-bound، محدود به Tool/Scope، دارای expiry، محافظت anti-replay/nonce و بررسی عضویت Subject در Tenant محول‌شده است؛ Mode چهارم یا Escalation نیست.

دو Audience پشتیبانی می‌شوند: Agent مدیریتی داخلی با `identity.mcp.admin.*` و Agent مجاز مشتری با Capability جاری `identity.mcp.self.*` و Context Delegated معتبر در صورت نیاز. Prompt، Metadata آزاد و تأیید طبیعی هرگز مجوز نیستند. Mutation حساس Approval دقیق برای Tool/Action/Resource/Tenant/Actor و Audit redact‌شده لازم دارد.

## قرارداد Tool اختصاصی Identity

Identity هر دو Tool نوع Atomic و Business را expose می‌کند. هر Tool باید Version، `kind`، `audience`، `risk`، `approvalRequired`، `requiredScopes`، Status، Exposure، Entry Point Application و خروجی redact‌شده داشته باشد. Business می‌تواند Handlerها را هماهنگ کند اما نباید Ruleهای Domain را تکرار کند. `call_identity_api`، Endpoint/SQL/Repository Tool عمومی و خروجی حساس خام ممنوع‌اند؛ Toolهای Security در MVP پیش‌فرض expose نمی‌شوند.

### قواعد اجرایی امنیت

- سرور برای هر Call یک `McpExecutionContext` تازه می‌سازد. `Actor`، `Subject`، `Tenant`، `Mode`، Delegation، Metadata Tool، Scopeهای لازم/مؤثر، وضعیت Approval و Correlation ID متعلق به سرور هستند.
- `Actor` و `Subject` هرگز از Prompt، Metadata دلخواه یا `subjectId` ارسالی Caller پذیرفته نمی‌شوند. `Actor` از Authentication معتبر می‌آید و سرور قبل از اجرای Application، Subject و Tenant هدف را Resolve و Authorize می‌کند.
- در Mode `self`، `actor = subject` است؛ ورودی Tool نباید `userId`، `tenantId`، `subjectId` یا `actor` داشته باشد و عملیات نمی‌تواند از کاربر/tenant عبور کند. جایگزینی Subject فقط از طریق Context امضاشده و محدود Delegation ممکن است.
- در Mode `admin`، Actor احراز‌شده باید Scope مدیریتی لازم و Context یکتای Tenant مجاز داشته باشد. User/Tenant/Session هدف سمت سرور Resolve می‌شود و باید داخل همان Tenant بماند. Tenant مفقود یا مبهم رد می‌شود و Admin Delegation نمی‌پذیرد.
- Scopeهای `security` جدا هستند و در MVP پیش‌فرض غیرفعال‌اند. مدیریت Client، Scope، Permission و Secret در Catalog فعلی expose نشده است.
- Context Delegated باید امضاشده، کوتاه‌مدت (حداکثر پنج دقیقه)، متصل به Audience `identity-service-mcp` و Tenant، دارای Nonce ضد Replay، محدود به Allowlist Tool/Scope و فقط در صورت عضویت Subject محول‌شده در Tenant مربوط طبق Application پذیرفته شود. Scope مؤثر اشتراک Scopeهای Authentication و Delegation است و Delegation حق را افزایش نمی‌دهد.
- `identity_revoke_user_sessions` Approval امضاشده و دقیق می‌خواهد که به Actor، Tenant، Audience، Tool/Version، Action، Resource، زمان انقضا و Nonce متصل باشد. Resource مربوط به همه Sessionهای دیگر، Session جاری احراز‌شده را نیز در صورت وجود Bind می‌کند. تأیید محاوره‌ای «بله» Approval نیست.
- هر Call پذیرفته‌شده، ردشده، ناموفق یا لغوشده با Actor، Subject، Tenant، Mode، نام/نسخه Tool، Scopeهای لازم/مؤثر، تصمیم Authorization، شناسه/وضعیت Approval، وضعیت نتیجه و Correlation ID Audit می‌شود. Token، Secret، OTP، مواد Signing، Credential دیتابیس و Payload بدون Redaction هرگز برگردانده یا Audit نمی‌شوند.

Catalog اجرایی Identity برای مجموعه قابلیت فعلی API کامل و نسخه‌دار است: ۳۶ Tool فعال (۳۴ Atomic و ۲ Business) قابلیت‌های Profile، Session، User، Role، Tenant، Permission، Scope، Client و Relationship را پوشش می‌دهند. Mapping کامل و محدودیت ورودی‌ها منبع حقیقت [Manifest ماشین‌خوان Identity MCP](../../.corevia/mcp/identity-service.yaml) است؛ پنج Descriptor عملیات پروتکلی فقط برای Inventory و غیرقابل‌Expose هستند.

نمونه‌ای از Toolهای اجرایی:

| Tool | نوع | Audience | Risk | Scope | مرز Application |
|---|---|---|---|---|---|
| `identity_get_self_profile` | atomic | self | read | `identity.mcp.self.read` | `GetProfileQuery` |
| `identity_list_self_sessions` | atomic | self | read | `identity.mcp.self.read` | `GetCurrentUserSessionsQuery` |
| `identity_get_user_access_summary` | business | admin | read | `identity.mcp.admin.read` | `GetUserByIdQuery`، `GetUserRolesQuery`، `GetUserTenantsQuery` |
| `identity_revoke_user_sessions` | business | admin | sensitive | `identity.mcp.admin.write` | `RevokeSessionCommand` یا `RevokeOtherSessionsCommand` |

Adapter این Toolها را از طریق پروتکل MCP منتشر می‌کند، اما Endpoint Mirror نمی‌سازد. Tool نوع Atomic یک قابلیت محدود Application و Tool نوع Business یک هدف مجاز است که می‌تواند چند Handler Application را هماهنگ کند. هیچ‌کدام نباید Ruleهای Domain را تکرار کنند یا Proxy عمومی باشند.

## قرارداد Transport و Federation Identity

- Local: HTTP احراز‌شده (Transport پیش‌فرض) فقط روی `127.0.0.1`/`::1`، با Authentication، بدون DNS عمومی/NAT/Port Forward/Reverse Proxy بدون Auth و با بررسی Host/Origin در صورت وجود.
- Customer Server: HTTPS احراز‌شده یا HTTP خصوصی با کنترل‌های معادل؛ Gateway مرکزی، تونل inbound عمومی و Internet dependency لازم نیست.
- Cloud AI: فقط از مسیر MCP Client/Desktop Connector محلی با Consent، Redaction و Egress Policy.
- آینده سازمانی: `Active Directory (AD/LDAP) -> Keycloak User Federation -> OIDC/OAuth2 -> Identity MCP / Gateway آینده`؛ MCP مستقیم به AD/LDAP وصل نمی‌شود.

قرارداد تنظیمات Runtime:

- `MCP_TRANSPORT=http|stdio` که پیش‌فرض آن `http` است. `stdio` فقط Transport فرآیندی مخصوص توسعه است و Host خارج از محیط Development با آن شروع نمی‌شود (`mcp_stdio_dev_only`).
- `COREVIA_MCP_BEARER_TOKEN` فقط برای Authentication فرآیند stdio پذیرفته می‌شود؛ HTTP در درخواست Anonymous هرگز به Token فرآیند Fallback نمی‌کند.
- `MCP_HTTP_BIND_ADDRESS` باید آدرس عددی باشد. پیش‌فرض Loopback است؛ دسترسی عمومی غیر Loopback به HTTPS نیاز دارد و HTTP خصوصی علاوه بر آن `MCP_PRIVATE_NETWORK_TRUSTED=true` صریح می‌خواهد.
- `MCP_HTTP_PORT`، `MCP_HTTP_HTTPS_ENABLED`، `MCP_ALLOWED_HOSTS` و `MCP_ALLOWED_ORIGINS` Profile کنترل‌شده HTTP را تنظیم می‌کنند.
- `MCP_DELEGATION_SIGNING_KEY` و `MCP_APPROVAL_SIGNING_KEY` فقط Secret Runtime از Vault/Environment هستند و در Source Configuration وجود ندارند.
- در HTTP، `/health` ناشناس و `/mcp` احراز هویت‌شده و مشمول بررسی Host/Origin است. در stdio، stdout فقط Frameهای پروتکل MCP و لاگ‌ها stderr هستند. Host مربوط به stdio Listener کستل ایجاد نمی‌کند.

## قرارداد ممیزی محلی
- `appsettings.json` بازبینی شد: بله (مقادیر غیرحساس fallback برای `Jwt`، `IdentityClient` و `Swagger` نگه داشته شد)
- فایل واقعی `.env` بازبینی شد: بله (Secrets محلی قدیمی شناسایی و حذف شدند)
- `docker-compose.yml` بازبینی شد: بله
- `Program.cs` بازبینی شد: بله (ترتیب Bootstrap اصلاح شد)
- کلاس‌های Option بازبینی شدند: بله (`JwtOptions`، `RootAdminOptions`، `IdentityClientOptions`)
- کلیدهای محلی تکراری/بدون‌استفاده حذف شدند: بله

## قرارداد دسته‌بندی
- کلیدهای کانفیگ در Consul:
	- `identity-db/provider`
	- `identity-db/command-timeout-seconds`
	- `jwt/issuer`
	- `jwt/audience`
	- `jwt/access-token-minutes`
	- `jwt/refresh-token-days`
	- `swagger/enabled`
	- `swagger/documents/0/{name,title,version,description}`
	- `swagger/security/enable-bearer`
	- `swagger/security/description`
	- `swagger/ui/{enable,route-prefix,expose-in-production,display-request-duration,enable-filter,default-models-expand-depth}`
	- `identity-client/default-public-client-id`
	- `identity-client/m2m-clients/<client-id>/name`
	- `identity-client/m2m-clients/<client-id>/description`
	- `identity-client/m2m-clients/<client-id>/scopes/<index>`
- کلیدهای محرمانه در Vault:
	- `POSTGRES_HOST`
	- `POSTGRES_PORT`
	- `IDENTITY_POSTGRES_DB`
	- `POSTGRES_USER`
	- `POSTGRES_PASSWORD` (الزامی و بدون مقدار پیش‌فرض؛ نبودن آن با `InvalidOperationException` و نام همین کلید خطا می‌دهد)
	- `SQLSERVER_HOST`
	- `SQLSERVER_PORT`
	- `IDENTITY_SQLSERVER_DB`
	- `SQLSERVER_DB`
	- `SQLSERVER_USER`
	- `SQLSERVER_PASSWORD` (بدون مقدار پیش‌فرض؛ فقط وقتی `SQLSERVER_USER` تنظیم شده الزامی است و نبودن آن با `InvalidOperationException` و نام همین کلید خطا می‌دهد؛ بدون نام کاربری و رمز، Integrated Security استفاده می‌شود)
	- `MSSQL_SA_PASSWORD`
	- `JWT_SECRET`
	- `IDENTITY_ROOT_PHONE`
	- `M2M_IDENTITY_SERVICE_SECRET`
	- `M2M_OTP_SERVICE_SECRET`
	- `M2M_ORDER_SERVICE_SECRET`
  - `M2M_INVOICE_SERVICE_SECRET`
  - `M2M_POS_SERVICE_SECRET`
	- `M2M_CATALOG_SERVICE_SECRET`
	- `M2M_DIDARSYNC_SERVICE_SECRET`
	- `M2M_CUSTOMER_SERVICE_SECRET`
	- `M2M_BASKET_SERVICE_SECRET`
	- `M2M_PAYMENT_SERVICE_SECRET`
	- `M2M_WOOCOMMERCE_SERVICE_SECRET`
  - `M2M_WOOSYNC_SERVICE_SECRET`
  - `M2M_NOPCOMMERCE_SERVICE_SECRET`
- کلاینت `woosync-service` قرارداد احراز سرویس برای provider ووکامرس در ریپوی مستقل `corevia-sync` است. متادیتا و scopeهای آن default غیرحساس IdentityClient هستند و secret آن باید فقط از Vault خوانده شود و هرگز commit نشود.
- Identity مقدار `pos.payment.start` را به `invoice-service` و مقدار `invoice.payment.confirm` را فقط به `pos-service` اختصاص می‌دهد.
- کلاینت‌های مالی، `M2M_INVOICE_SERVICE_SECRET` و `M2M_POS_SERVICE_SECRET` را به‌عنوان ورودی اجباری seed اعلام می‌کنند. اگر secret مبتنی بر Vault یا scope اجباری در دسترس نباشد، راه‌اندازی Identity به‌جای حذف بی‌صدای کلاینت متوقف می‌شود.
- آیتم‌های Service Discovery در Consul:
	- نام رجیستری سرویس: `identity-service`
	- شناسه رجیستری سرویس: `nikplus-prd-online-shop-identity-service-5270`
	- شناسه چک سرویس: `service:nikplus-prd-online-shop-identity-service-5270`
	- مسیر Health: `/health`
	- مسیر Readiness: `/health/ready` (بررسی اتصال به پایگاه داده با EF Core و timeout سه ثانیه‌ای؛ در حالت در دسترس `200` و در غیر این صورت `503`). Host از نوع HTTP برای Mcp هم همین دو مسیر را به‌صورت ناشناس ارائه می‌دهد.
	- پورت سرویس: `5270`
	- پیکربندی consumer discovery برای وابستگی OTP:
		- `service-discovery/services/otp/service-name`
		- `service-discovery/services/otp/service-names`
		- `service-discovery/services/otp/scheme`
- کلیدهای Bootstrap محلی باقی‌مانده:
	- `CONSUL_HTTP_ADDR`
	- `CONSUL_HTTP_TOKEN`
	- `CONFIG_CENTER_PREFIX`
	- `VAULT_ADDR`
	- `VAULT_TOKEN`
	- `SECRET_STORE_PATH`
	- `IDENTITY_APP_VERSION`
	- `OTP_BASE_URL`
	- `OTP_HTTP_TIMEOUT_SECONDS`

## قرارداد Fallback برای Discovery Consumer
- IdentityService باید endpoint مربوط به وابستگی OTP را در حالت اصلی از `ServiceDiscovery:Services:Otp` resolve کند.
- اگر provider discovery در دسترس نبود یا endpoint سالم پیدا نشد، سرویس باید به `OTP_BASE_URL` از `IConfiguration` (env لودشده) fallback کند.
- failure در discovery باید با warning قابل مشاهده log شود و در صورت وجود کلید fallback، نباید startup پایه سرویس را متوقف کند.
- انتخاب endpoint مربوط به OTP فقط در زمان ثبت HttpClient انجام می‌شود. کلاینت `OtpRestClient` (provider نام‌دار `IdentityOtp` در Corevia.Kit.Http) باید از `HttpClient.BaseAddress` تنظیم‌شده استفاده کند و نباید دوباره `OTP_BASE_URL` را بخواند.
- timeout درخواست OTP با `OTP_HTTP_TIMEOUT_SECONDS` یا `IdentityOtp:TimeoutSeconds` کنترل می‌شود؛ مقدار نامعتبر یا missing به پیش‌فرض ۸ ثانیه برمی‌گردد.
- timeout در downstream OTP باید به خطای upstream کنترل‌شده تبدیل شود و نباید cancellation خام یا بی‌پاسخ ماندن درخواست frontend تولید کند.

## قرارداد Fallback
- مقادیر غیرحساس fallback در `appsettings.json` عمدا نگه داشته می‌شوند تا در اختلال موقت Providerها، سرویس قابل اجرا بماند.
- در حالت سلامت Providerها، مقادیر Consul اولویت دارند و fallback منبع اصلی production محسوب نمی‌شود.
- اگر `IdentityDb:Provider` یا `DB_PROVIDER` تنظیم نشده باشد، انتخاب Provider دیتابیس به Postgres برمی‌گردد تا رفتار فعلی Production حفظ شود.
- SQL Server فقط وقتی فعال می‌شود که مقدار Provider برابر `SqlServer` یا `Sql` باشد و credentialهای SQL Server از Vault/environment resolve شده باشند.

## قرارداد نام‌گذاری
- Prefix در Consul:
	- `nikplus/online-shop/prd/identity-service`
- مسیر Secret در Vault:
	- `nikplus/online-shop/prd/identity-service`
- قالب کلیدهای Secret:
	- `UPPER_CASE` با underscore تک
- شناسه‌های عملیاتی Discovery:
	- `ServiceID=nikplus-prd-online-shop-identity-service-5270`
	- `CheckID=service:nikplus-prd-online-shop-identity-service-5270`

## نمونه مسیرهای Consul
- `identity-client/m2m-clients/order-service/scopes/0`
- `identity-client/m2m-clients/nopcommerce-service/name`

## قرارداد ترتیب اجرا
- پاکسازی محلی قبل از Provisioning متمرکز: بله
- ایجاد/به‌روزرسانی Consul/Vault/Discovery قبل از هر تغییر CI/CD: بله
- اعتبارسنجی دسترسی Provider و سلامت Discovery قبل از CI/CD: بله
- قرارداد متغیرهای CI:
	- استفاده از شش کلید مشترک: `CONSUL_HTTP_ADDR`, `CONSUL_HTTP_TOKEN`, `CONFIG_CENTER_PREFIX`, `VAULT_ADDR`, `VAULT_TOKEN`, `SECRET_STORE_PATH`
	- حذف کلیدهای service-specific
	- مشتق‌سازی suffix سرویس به شکل `<base>/identity-service`

## قرارداد Bootstrap
- `AddConfigLoaderExtension(...)` قبل از Options Binding: بله
- `AddConfigLoaderExtension(...)` قبل از ثبت dependencyهای وابسته به کانفیگ: بله

## قرارداد مدیریت خطا
- نبودن کلیدهای الزامی Provider: Startup/Deploy باید fail شود.
- نبودن credential الزامی برای Provider دیتابیس انتخاب‌شده: Startup باید fail شود.
- خطاهای دسترسی `401/403` به Provider: مهاجرت کامل محسوب نمی‌شود.
- خطای Prefix/Path: باید قبل از انتشار اصلاح شود.
- خطای رجیستری Discovery: مهاجرت کامل محسوب نمی‌شود.

## قرارداد اعتبارسنجی
- خوانایی Provider با credential واقعی: بله
- تزریق در `IConfiguration`: بله
- رجیستری Discovery و سلامت `/health`: بله
- شناسه‌های عملیاتی `ServiceID/CheckID` در Consul اعتبارسنجی شد: بله
- ساختار M2MClients در Consul اعتبارسنجی شد: بله (`identity-client/m2m-clients/*`)

## آخرین به‌روزرسانی
- 2026-06-12

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
