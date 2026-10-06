# شواهد پیاده‌سازی Runtime مربوط به MCP در IdentityService

[English](./mcp-runtime-implementation-evidence.md) | [فهرست Specها](./README.fa.md) | [فهرست مخزن](https://gitlab.getcorevia.ir/corevia/market/-/blob/develop/docs/INDEX.fa.md)

> وضعیت: Baseline پیاده‌سازی فعال؛ Pilot کنترل‌شده باقی‌مانده است
> نسخه: ۰.۱.۰
> مالک: تیم IdentityService
> آخرین به‌روزرسانی: 2026-08-29

## هدف

ثبت شواهد عینی اینکه Runtime مربوط به Sibling MCP در IdentityService مطابق استاندارد Corevia پیاده‌سازی شده است؛ در عین حال CI راه‌دور، Pilot مشتری، انتشار Package و Deploy Production همچنان گیت‌های صریح Release هستند.

## دامنه

این سند فقط `IdentityService/IdentityService.Mcp` در `corevia-market` را پوشش می‌دهد:

- Composition به‌صورت Sibling روی Application و Domain مربوط به Identity؛
- احراز هویت، Resolve کردن Actor/Subject/Tenant در سمت سرور، Policyهای Self/Admin/Delegated، اشتراک Scopeها، Approval، Audit و Redaction؛
- کاتالوگ کامل قابلیت‌های Identity: تعداد ۴۱ ورودی قابلیت API، تعداد ۳۶ Tool فعال و قابل‌Expose MCP شامل ۳۴ Atomic و ۲ Business، و ۵ Binding صریح عملیات پروتکلی غیرقابل‌Expose؛
- Runtime محلی `stdio` و پیاده‌سازی Transport کنترل‌شده HTTP؛
- بررسی‌های Source، تست و مستندات روی برنچ اختصاصی پیاده‌سازی.

این سند ادعای Pilot مشتری، انتشار Package، Deploy Production، استقرار Gateway مرکزی یا Integration با AD/LDAP/Keycloak ندارد.

## پیش‌نیازها

- `.NET SDK 9` و Credential خصوصی Package در صورت Restore از Nexus؛
- `JWT_SECRET` معتبر که فقط از Environment/Vault تأمین شود و هرگز در Source Commit نشود؛
- برای Callهای Admin، Token معتبر با یک Claim یکتای `tenant_id`؛ Token کاربر آن را از Membership یگانه/پیش‌فرض و Token M2M از Binding صریح Tenant در Client می‌گیرد؛
- برای عملیات Delegated یا حساس، کلیدهای Signing و Envelopeهای امضاشده و تأییدشده به‌صورت جداگانه؛
- دسترسی به مخزن Corevia Standard برای اعتبارسنجی Manifest و Patternها.

## تنظیمات

فایل `IdentityService.Mcp/appsettings.json` فقط Defaultهای غیرحساس دارد. Overrideهای Runtime از Config متمرکز یا Environment تأمین می‌شوند:

| تنظیم | کاربرد |
|---|---|
| `MCP_TRANSPORT` | مقدار `http` (پیش‌فرض) یا `stdio` (فقط توسعه؛ بدون `ASPNETCORE_ENVIRONMENT=Development` رد می‌شود) |
| `COREVIA_MCP_BEARER_TOKEN` | Bearer Token کوتاه‌مدت مخصوص stdio؛ هرگز از Prompt یا Argument خط فرمان دریافت نمی‌شود |
| `MCP_HTTP_BIND_ADDRESS` | آدرس عددی Bind؛ پیش‌فرض Loopback است |
| `MCP_HTTP_PORT` | پورت Transport HTTP؛ پیش‌فرض `5271` |
| `MCP_HTTP_HTTPS_ENABLED` | فعال‌سازی HTTPS برای Bind غیر Loopback/سرور مشتری |
| `MCP_PRIVATE_NETWORK_TRUSTED` | Assertion صریح اپراتور برای HTTP خصوصی غیر Loopback |
| `MCP_ALLOWED_HOSTS` / `MCP_ALLOWED_ORIGINS` | Allowlist مربوط به Host/Origin برای `/mcp` |
| `ASPNETCORE_Kestrel__Certificates__Default__Path` | مسیر Certificate مربوط به HTTPS سرور مشتری که فقط از Config/Secret Provider تأمین می‌شود |
| `ASPNETCORE_Kestrel__Certificates__Default__Password` | رمز Certificate HTTPS سرور مشتری؛ مقدار `<set via vault://>` که فقط از Secret Provider تأمین می‌شود |
| `MCP_DELEGATION_SIGNING_KEY` | کلید فقط از Vault/Environment برای Contextهای Delegated امضاشده |
| `MCP_APPROVAL_SIGNING_KEY` | کلید فقط از Vault/Environment برای Approval دقیق عملیات حساس |

Seed مربوط به Identity همه Scopeهای `identity.mcp.*` و Permissionهای آن‌ها را ثبت می‌کند و فقط `identity.mcp.self.read` را به Client عمومی پیش‌فرض می‌دهد. Scopeهای Admin قابلیت صریح هستند و تخصیص Scope بدون Binding Tenant، Call Admin را معتبر نمی‌کند. کاتالوگ قابلیت‌های API، مجوزهای Authorization را از عملیات پروتکلی جدا می‌کند و عملیات پروتکلی در صورت خطر خروج Token یا Secret خام، صریحاً غیرقابل‌Expose باقی می‌مانند.

فرآیند MCP Extensionهای ConfigCenter/SecretStore مربوط به Corevia را فقط در Composition Root بارگذاری می‌کند. کد `IdentityService.Mcp` مستقیماً Database، Repository، SQL یا API Route را نمی‌خواند.

## اجرا / استفاده

### نصب Local و Offline

`stdio` فقط Transport فرآیندی توسعه است (نیازمند `ASPNETCORE_ENVIRONMENT=Development`؛ پیش‌فرض HTTP است). MCP Client فرآیند را اجرا می‌کند و Frameهای JSON-RPC را از طریق ورودی/خروجی استاندارد ردوبدل می‌کند:

```sh
cd IdentityService/IdentityService.Mcp
JWT_SECRET='<redacted>' \
COREVIA_MCP_BEARER_TOKEN='<short-lived-token>' \
ASPNETCORE_ENVIRONMENT=Development \
MCP_TRANSPORT=stdio \
dotnet run
```

لاگ‌های برنامه روی stderr نوشته می‌شوند و stdout فقط برای Frameهای پروتکل MCP رزرو است. AI ابری در حالت Offline مستقیماً به این Process دسترسی ندارد و باید از MCP Client/Desktop Connector محلی مورد اعتماد و Egress مورد تأیید مشتری استفاده کند.

### HTTP روی Loopback یا سرور مشتری

برای HTTP مقدار `MCP_TRANSPORT=http` را تنظیم کنید. HTTP روی Loopback همچنان احراز هویت‌شده است و فقط روی `127.0.0.1` یا `::1` Bind می‌شود. دسترسی عمومی/سرور مشتری به HTTPS نیاز دارد؛ HTTP خصوصی نیازمند تنظیم صریح Trusted Network و کنترل معادل شبکه است. Endpoint `/health` برای Health Check ناشناس است؛ `/mcp` احراز هویت و Policy مربوط به Host/Origin را الزامی می‌کند.

در وضعیت فعلی هیچ Dependencyای به Corevia MCP Gateway وجود ندارد. Gateway مدیریتی آینده باید Relay/Policy Layer اختیاری باقی بماند و نباید مرز Application یا Authorization مشتری را دور بزند.

## اعتبارسنجی / تصدیق

شواهد زیر روی Worktree/Branch اختصاصی پیاده‌سازی MCP Identity به‌دست آمده است:

| بررسی | نتیجه | شاهد |
|---|---|---|
| مرز Sibling | PASS | `IdentityService.Mcp` در Composition فقط به Application/Infrastructure Reference دارد؛ Reference به API و دسترسی Controller/API/EF/Repository/SQL در کد MCP وجود ندارد. |
| Build Release | PASS | `dotnet build IdentityService/IdentityService.Mcp/IdentityService.Mcp.csproj` بعد از جداسازی Transport با Generic Host با ۰ Warning و ۰ Error تمام شد. |
| تست Runtime/Security | PASS | پروژه `IdentityService.Mcp.Tests`: Suite کامل Catalog و Security با ۰ خطا موفق شد. |
| Smoke پروتکل | PASS | `initialize` و `tools/list` روی stdio موفق بود؛ دقیقاً ۳۶ Tool فعال منتشر شد و stdout فقط Frameهای JSON-RPC MCP داشت. |
| Smoke منفی HTTP | PASS | روی Loopback و پورت `۵۲۹۱`، `/health` پاسخ `۲۰۰`، `/mcp` ناشناس پاسخ `۴۰۱` و Host نامعتبر پاسخ `۴۰۰` داد و Process بدون خطا متوقف شد. |
| Guard مربوط به Bind عمومی | PASS | Bind غیر Loopback بدون `MCP_PRIVATE_NETWORK_TRUSTED=true` قبل از شروع Listener رد شد. |
| پروفایل HTTPS سرور مشتری | PASS | اجرای ایزوله HTTPS با Certificate موقت از `/health` پاسخ `۲۰۰` و از `/mcp` ناشناس پاسخ `۴۰۱` گرفت. |
| پروفایل HTTP خصوصی مشتری | PASS | اجرای ایزوله HTTP غیر Loopback با `MCP_PRIVATE_NETWORK_TRUSTED=true` صریح از `/health` پاسخ `۲۰۰` و از `/mcp` ناشناس پاسخ `۴۰۱` گرفت. |
| برابری مجوز API/MCP | PASS | ۴۱ ورودی قابلیت API به‌صورت یک‌به‌یک Mapping شده‌اند؛ ۳۶ مجوز Authorization Tool فعال و قابل‌استفاده دارند و ۵ عملیات پروتکلی با `mcpExposure: prohibited` صریحاً غیرقابل‌Expose هستند. محدودیت‌ها، Grantها و Approval عملیات حساس با Validator استاندارد بررسی می‌شوند. |
| Policy Tool | PASS | ۳۶ Tool فعال شامل ۳۴ Atomic و ۲ Business، Metadata نسخه‌دار Audience/Risk/Scope/Approval/Restriction دارند؛ Proxy عمومی API وجود ندارد. |
| Policyهای Self/Admin/Delegated | PASS | تست‌های Selector Injection، Scope مفقود، مرز Tenant، بررسی عضویت Subject در Delegation، Audience/Expiry، Replay در Delegation و Resolve سمت سرور Session جاری اجرا شدند. |
| اتصال Scope و Tenant در Identity | PASS | Scope/Permissionهای MCP Seed می‌شوند؛ Self read برای Client عمومی صریح است؛ Token کاربر Claim یکتای Tenant از Membership/Default می‌گیرد و Binding Tenant مربوط به M2M از تنظیمات می‌آید. |
| Approval/Redaction/Audit | PASS | Binding دقیق Approval، Binding Session جاری برای لغو همه Sessionها، مصرف Nonce، DTOهای امن خروجی و Audit خطا در کد و تست پوشش دارد. |
| بی‌طرفی Deployment | PASS | stdio با Generic Host بدون Listener کستل اجرا می‌شود؛ HTTP اختیاری و محافظت‌شده است؛ Gateway مرکزی لازم نیست. |
| CI/MR راه‌دور | PENDING | پس از Push باید روی Branch/Merge Request سبز شود تا Phase 7 بسته شود. |
| Pilot/Release مشتری | PENDING | به Approval کنترل‌شده و شواهد مشتری جداگانه نیاز دارد و از تست محلی نتیجه نمی‌شود. |

Validator کل Repository در حال حاضر ۲۴ Finding قدیمی و نامرتبط در فایل‌های Legacy/Config/Documentation گزارش می‌کند. در فایل‌های تغییرکرده MCP Finding جدیدی گزارش نشده است؛ مالک پاک‌سازی این Baseline، Platform Engineering است و موضوع به‌عنوان Gate خارجی Release باقی می‌ماند، نه Finding مربوط به Implementation MCP.

Documentation Audit کل Repository نیز دو خطای قدیمی Link مربوط به Placeholder در `docs/templates/TECHNICAL_DOCUMENT_TEMPLATE.fa.md` و Warningهای Section قدیمی گزارش می‌کند. Docs تغییرکرده Identity MCP خطای جدید Pair یا Link ایجاد نکرده‌اند و Gate مربوط به MR به‌صورت changed-only اجرا می‌شود.

دستورهای اصلی اعتبارسنجی محلی:

```sh
NUGET_PACKAGES='<workspace>/.codex-nuget-cache' \
NUGET_HTTP_CACHE_PATH='<workspace>/.codex-nuget-http-cache' \
dotnet build IdentityService/IdentityService.Mcp/IdentityService.Mcp.csproj --no-restore

NUGET_PACKAGES='<workspace>/.codex-nuget-cache' \
NUGET_HTTP_CACHE_PATH='<workspace>/.codex-nuget-http-cache' \
dotnet test IdentityService/IdentityService.Mcp.Tests/IdentityService.Mcp.Tests.csproj --no-restore
```

Job استاندارد CI علاوه بر این، با `.ci/create-nuget-config.sh` از Nexus احراز‌شده Restore می‌کند، Build Release و Test را اجرا می‌کند، Referenceهای پروژه را فهرست می‌کند و در صورت ورود `IdentityService.Api` یا Symbolهای دسترسی مستقیم ممنوع، Job را Fail می‌کند.

## رفع اشکال

- اگر خروجی stdio JSON-RPC معتبر نبود، تنظیمات لاگ stderr را بررسی کنید؛ لاگ برنامه نباید به stdout نوشته شود.
- اگر درخواست ناشناس به `/mcp` رسید، JWT Bearer و `RequireAuthorization()` را بررسی کنید و Authentication را خاموش نکنید.
- اگر Bind غیر Loopback شکست خورد، HTTPS یا تنظیم صریح Trust شبکه خصوصی را Provision کنید؛ Listener عمومی بدون احراز هویت ایجاد نکنید.
- اگر Envelope Delegation یا Approval رد شد، Audience، Tenant، Tool/Version، Scope، Expiry، Nonce و Resource دقیق را بررسی کنید. Prompt یا `subjectId` دلخواه جایگزین Envelope معتبر نیست.
- در Deployment چند Replica، پیش از فعال‌سازی ترافیک مشترک Delegation/Approval، In-memory Nonce Store باید با Store توزیع‌شده ضد Replay جایگزین شود.
- در صورت شکست Restore Package خصوصی، از مسیر Nexus احراز‌شده در CI استفاده کنید و Credential یا آدرس Registry بازنشسته را Commit نکنید.

## سابقه تغییر

- 2026-08-29: شواهد Runtime با Catalog کامل برابری ۴۱تایی API/MCP، ۳۶ Tool فعال، پنج Binding صریح غیرقابل‌Expose و Gateهای قابل‌استفاده مجدد Standard/Bridge همگام شد.
- 2026-08-28: اولین شواهد پیاده‌سازی Runtime مربوط به Sibling MCP Identity شامل Build، Test، Smoke پروتکل، بررسی امنیت و مرز Deployment ثبت شد.

## مالکیت

تیم IdentityService؛ مالک استاندارد قابل استفاده مجدد MCP و Validatorهای عمومی، Platform Engineering است.

## راستی‌آزمایی پس از استخراج (مرحله 7 از Phase C، 2026-10-04)

- اجرای `corevia-run validate --pattern mcp-contract.validate` با `.corevia/operations/identity-service-mcp-contract.values.yaml`: نتیجه `contract-valid`.
- اجرای `corevia-run validate --pattern mcp-permission-parity.validate` با `.corevia/operations/identity-service-mcp-parity.values.yaml`: نتیجه `permission-parity-valid` (۴۱ ورودی Catalog، ۳۶ Permission فعال هرکدام با Tool قابل‌استفاده یک‌به‌یک، ۵ عملیات پروتکلی ممنوع و بدون Tool یتیم).
- شکاف یافت‌شده و اصلاح‌شده: کد `MCP_TRANSPORT` را به‌طور پیش‌فرض `stdio` می‌گرفت. پروفایل استاندارد، HTTP را پیش‌فرض و stdio را فقط برای توسعه می‌خواهد؛ `McpTransportSelector` اکنون پیش‌فرض `http` دارد و `stdio` را خارج از `ASPNETCORE_ENVIRONMENT=Development` رد می‌کند (`BadRequestException` با کد `mcp_stdio_dev_only`). با `McpTransportSelectorTests` پوشش داده شده است.
- `dotnet test IdentityService.Mcp.Tests`: ۲۷ تست موفق (۱۶ تست قبلی به‌علاوه ۱۱ حالت انتخاب Transport).
