# پذیرش مهاجرت Runtime در IdentityService

[English](./acceptance.md) | [فارسی](./acceptance.fa.md)

> وضعیت: Baseline پیاده‌سازی فعال؛ Pilot کنترل‌شده باقی‌مانده است
> نسخه: ۰.۱۱.۱
> مالک: IdentityService

## نگاشت به استاندارد MCP Corevia

Baseline قابل استفاده مجدد پذیرش MCP در [سناریوهای A01 تا A16 استاندارد MCP Corevia](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/acceptance.fa.md) تعریف شده است. این فایل فقط سناریوهای اجرایی اختصاصی Identity را نگه می‌دارد و آن‌ها را به Baseline عمومی map می‌کند؛ نباید Requirementهای منفی عمومی را ضعیف یا جایگزین کند. Profile ماشین‌خوان در [`.corevia/mcp/identity-service.yaml`](../../.corevia/mcp/identity-service.yaml) و اعتبارسنجی آن در [Values قرارداد MCP Identity](../../.corevia/operations/identity-service-mcp-contract.values.yaml) است.

## سناریو ۰: طول عمر نشست مشتری
- با فرض اینکه مشتری ورود OTP را کامل کرده است
- هرگاه access token منقضی شود و مرورگر یک API محافظت‌شده را فراخوانی کند
- کلاینت باید بتواند refresh token را بدون اجبار به ورود مجدد بچرخاند
- و نشست تا خروج مشتری یا لغو صریح نشست معتبر بماند
## سناریو 1: ممیزی و پاکسازی محلی
- Given:
	- فایل‌های `appsettings.json`، `.env` واقعی، `docker-compose.yml`، `Program.cs` و Optionها بازبینی شده‌اند
- When:
	- کلیدهای تکراری/بدون‌استفاده حذف می‌شوند و فقط مقادیر Bootstrap غیرحساس باقی می‌مانند
- Then:
	- هیچ Secret سخت‌کد شده‌ای در `.env` محلی باقی نمی‌ماند
	- `appsettings.json` فقط مقادیر غیرحساس دارد

## سناریو 2: کانفیگ و Secret متمرکز
- Given:
	- Prefix مربوط به `identity-service` در Consul و مسیر Secret در Vault تعریف شده است
- When:
	- سرویس با `CONSUL_*`، `VAULT_*`، `CONFIG_CENTER_PREFIX` و `SECRET_STORE_PATH` معتبر اجرا می‌شود
- Then:
	- تنظیمات غیرحساس JWT/IdentityClient از Consul خوانده می‌شوند
	- Secrets دیتابیس/JWT/M2M از Vault خوانده می‌شوند
	- متادیتای `IdentityClient:M2MClients` (فیلدهای `Name`، `Description` و `Scopes`) از ساختار سلسله‌مراتبی Consul خوانده می‌شود
	- متادیتای `IdentityClient:M2MClients` (فیلدهای `Name`، `Description` و `Scopes`) از ساختار سلسله‌مراتبی Consul خوانده می‌شود
	- متادیتای کلاینت `woosync-service` به‌عنوان baseline غیرحساس برای provider ووکامرس Corevia Sync در دسترس می‌ماند
	- `M2M_WOOSYNC_SERVICE_SECRET` از Vault خوانده می‌شود و دقیقاً همان client id قراردادی `woosync-service` را seed می‌کند
	- متادیتای کلاینت و Secret مربوط به DidarSync (`didarsync-service` / `M2M_DIDARSYNC_SERVICE_SECRET`) درست خوانده می‌شود
	- متادیتا و secret کلاینت‌های Invoice/POS درست خوانده می‌شود؛ `invoice-service` دارای `pos.payment.start` و `pos-service` فقط دارای `invoice.payment.confirm` است
	- اگر secret یا scope اجباری Invoice/POS در دسترس نباشد، راه‌اندازی سرویس متوقف می‌شود

## سناریو 3: Discovery و Bootstrap
- Given:
	- رجیستری `identity-service` در Consul وجود دارد و شناسه‌های عملیاتی استاندارد است:
	- `ServiceID=nikplus-prd-online-shop-identity-service-5270`
	- `CheckID=service:nikplus-prd-online-shop-identity-service-5270`
- When:
	- سرویس بالا می‌آید و `/health` پایدار است
- Then:
	- وضعیت Discovery سالم و قابل مسیردهی است
	- ترتیب Bootstrap برابر است با: `base sources -> AddConfigLoaderExtension(...) -> options/dependent registrations`

## سناریو 4: قابلیت اجرا در حالت Fallback
- Given:
	- دسترسی به Providerهای Consul/Vault به‌صورت موقت قطع است
- When:
	- سرویس با bootstrap env محلی و مقادیر غیرحساس appsettings اجرا می‌شود
- Then:
	- startup پایه سرویس موفق باقی می‌ماند
	- لاگ هشدار فعال شدن fallback بدون افشای secret ثبت می‌شود

## سناریو 5: Fallback برای Discovery وابستگی OTP
- Given:
	- `ServiceDiscovery:Services:Otp` پیکربندی شده و `OTP_BASE_URL` در environment موجود است
- When:
	- provider مربوط به discovery در دسترس نیست یا endpoint سالم OTP برنمی‌گرداند
- Then:
	- IdentityService برای تماس‌های OTP از fallback با `OTP_BASE_URL` استفاده می‌کند
	- سرویس عملیاتی می‌ماند و warning مربوط به fallback در لاگ ثبت می‌شود
	- `OtpRestClient` مقدار BaseAddress انتخاب‌شده در زمان ثبت HttpClient را override نمی‌کند
	- timeout در downstream OTP قبل از abort شدن درخواست frontend به خطای upstream کنترل‌شده تبدیل می‌شود

## سناریو 6: پذیرش صریح صف OTP
- با فرض اینکه:
	- OTPService کد را ذخیره و notification را برای ارسال پس‌زمینه می‌پذیرد
- وقتی:
	- `POST /v1/api/Identity/Send` تکمیل می‌شود
- آنگاه:
	- Identity پاسخ `202 Accepted` با `accepted=true` و `delivery=queued` برگرداند
	- client بدون انتظار برای provider پیامک وارد مرحله ورود کد شود
	- client payload خالی یا غیرمنتظره را موفقیت تلقی نکند

## سناریو 7: Scope خواندن مانده مشتری برای PaymentService
- Given:
	- `payment-service` به عنوان کلاینت M2M تعریف شده است
	- orchestration بعد از پرداخت در فلوی شارژ کیف پول یا خرید آنلاین برای پارامترهای پیامک به مانده حساب مشتری نیاز دارد
- When:
	- PaymentService توکن می‌گیرد و endpoint `GET /v1/api/Customer/ByPhone/{phoneNumber}?fresh=true` در CustomerService را صدا می‌زند
- Then:
	- توکن صادرشده شامل scope `customer.read` است
	- CustomerService درخواست را با policy `CustomerReadAccess` مجاز می‌کند
	- ثبت رسید کامل باقی می‌ماند و خواندن مانده برای پیامک با خطای `403 Forbidden` متوقف نمی‌شود

## سناریو 6: آداپتر Provider دیتابیس
- Given:
	- `IdentityDb:Provider` یا `DB_PROVIDER` تنظیم نشده است
- When:
	- IdentityService اجرا می‌شود
- Then:
	- مسیر Provider فعلی Postgres استفاده می‌شود
	- رفتار migration و seed موجود تغییر نمی‌کند
- Given:
	- `IdentityDb:Provider` یا `DB_PROVIDER` برابر `SqlServer` است
- When:
	- Secretهای اتصال SQL Server از Vault/environment resolve می‌شوند
- Then:
	- IdentityService از آداپتر EF Core SQL Server استفاده می‌کند
	- رفتار seed هویت تغییر نمی‌کند
	- دیتابیس موجود SQL Server پیش از اجرای queryهای token/introspection با ستون‌های افزایشی موردنیاز Identity به‌روزرسانی می‌شود
	- این به‌روزرسانی idempotent است و داده‌های هویت را حذف یا بازنویسی نمی‌کند

## سناریو ۷: مرز Sibling مربوط به MCP

- با فرض اینکه:
	- `IdentityService.Api` و `IdentityService.Mcp` روی همان لایه‌های Application/Domain compose شده‌اند
- هرگاه:
	- یک Tool مربوط به MCP فراخوانی شود
- آنگاه:
	- Adapter مربوط به MCP مستقیماً Use Case/Handler MediatR لایه Application را فراخوانی می‌کند
	- هیچ کد MCP، Controller، endpoint HTTP، مسیر Gateway/Ocelot، EF Core یا Repository را مستقیماً فراخوانی نمی‌کند
	- Assemblyهای Application و Domain به هیچ‌یک از Adapterهای انتقال وابسته نیستند

## سناریو ۸: مجوز Self در MCP

- با فرض اینکه:
	- MCP Client مشتری Scope `identity.mcp.self.read` یا `identity.mcp.self.write` دارد
- هرگاه:
	- مشتری یک Tool مربوط به خودش را همراه `userId`، `tenantId` یا `subjectId` متعلق به هویت دیگر فراخوانی کند
- آنگاه:
	- درخواست رد می‌شود
	- Subject مؤثر همان کاربر احراز‌شده جاری باقی می‌ماند
	- هیچ داده‌ای بین کاربران یا Tenantها خوانده یا تغییر داده نمی‌شود
	- Token، Secret، کد OTP، مواد امضا و credential دیتابیس خام در خروجی Tool وجود ندارد

## سناریو ۹: مجوز Admin و مرز Tenant در MCP

- با فرض اینکه:
	- یک Agent مدیریتی داخلی Context احراز هویت معتبر و `identity.mcp.admin.read` یا `identity.mcp.admin.write` دارد
- هرگاه:
	- Agent یک Subject از نوع User/Tenant/Session را درخواست کند
- آنگاه:
	- Actor از Context احراز هویت معتبر استخراج می‌شود، نه از Prompt یا metadata دلخواه
	- سرور Subject را نسبت به Tenant و مجوزهای مؤثر Actor resolve و authorize می‌کند
	- نبودن یا مبهم بودن Tenant Context به‌جای حدس‌زدن رد می‌شود
	- Write حساس فقط با Approval متصل به Tool/Action/Resource دقیق اجرا می‌شود

## سناریو ۱۰: Context اجرای Delegated در MCP

- با فرض اینکه:
	- Agent به نمایندگی از مشتری عمل می‌کند
- هرگاه:
	- عملیات delegated در MCP فراخوانی شود
- آنگاه:
	- Subject فقط از Context delegation کوتاه‌مدت، امضاشده و audience-bound resolve می‌شود
	- Context با expiry، audience، tenant، Tool/Scopeهای مجاز، داده anti-replay/nonce و عضویت تأییدشده Subject محول‌شده در Tenant مربوط محدود می‌شود
	- `subjectId` ارسالی Caller به‌تنهایی مجوز ایجاد نمی‌کند
	- delegation مجوز مؤثر مشتری را گسترش نمی‌دهد

## سناریو ۱۱: قرارداد Tool در MCP

- با فرض اینکه:
	- یک Tool مربوط به Identity MCP فعال است
- هرگاه:
	- Schema آن برای MCP Client منتشر شود
- آنگاه:
	- Tool باید `kind` (`atomic` یا `business`)، `audience`، `risk`، `approvalRequired` و `requiredScopes` را اعلام کند
	- Atomic Tool یک قابلیت محدود و نزدیک به Use Case Application ارائه می‌کند
	- Business Tool یک هدف کاربر/Agent را ارائه می‌کند و می‌تواند چند Use Case Application را هماهنگ کند
	- هر دو نوع Tool از Handlerهای Application و قواعد Domain استفاده می‌کنند
	- `call_identity_api` عمومی، endpoint دلخواه، SQL و Repository Tool در دسترس نیست

## سناریو ۱۲: Audit و Redaction در MCP

- با فرض اینکه:
	- یک فراخوانی MCP پذیرفته، رد، approve یا fail شده است
- هرگاه:
	- رکورد اجرا نوشته شود
- آنگاه:
	- رکورد شامل Actor، Subject، Tenant، Mode، نام/نسخه Tool، Scopeهای لازم/مؤثر، تصمیم Authorization، شناسه/وضعیت Approval، وضعیت نتیجه و correlation id است
	- Token، Secret، کد OTP، کلید امضا و payload حساس بدون redaction ثبت نمی‌شود

## سناریو ۱۳: نصب Local و Cloud Client برای MCP

- با فرض اینکه:
	- `IdentityService.Mcp` روی سیستم محلی مشتری بدون دسترسی اینترنت نصب شده است
- هرگاه:
	- MCP Client محلی آن را فراخوانی کند
- آنگاه:
	- `stdio` بدون Gateway مرکزی یا اینترنت کار می‌کند
	- اگر HTTP روی loopback فعال باشد، فقط روی `127.0.0.1`/`::1` bind می‌شود، Authentication/Authorization را حفظ می‌کند و public forwarding ندارد

- با فرض اینکه:
	- هوش مصنوعی Cloud به نصب Local نیاز دارد
- هرگاه:
	- یک MCP Client/Desktop Connector محلی و مورد اعتماد فراخوانی را relay کند
- آنگاه:
	- هوش مصنوعی Cloud فقط از مسیر همان Connector به MCP می‌رسد
	- MCP آفلاین مستقیماً از Cloud قابل دسترسی نیست
	- داده relay‌شده تابع Consent مشتری، redaction و Policy خروج داده است

## سناریو ۱۴: نصب MCP روی سرور مشتری

- با فرض اینکه:
	- `IdentityService.Mcp` روی سرور مشتری نصب شده است
- هرگاه:
	- MCP Client راه دور متصل شود
- آنگاه:
	- دسترسی عمومی با HTTPS احراز‌شده یا HTTP خصوصی محدود به شبکه قابل اعتماد و کنترل‌های معادل انجام می‌شود
	- نصب بدون Gateway مرکزی Corevia، تونل inbound عمومی یا وابستگی اینترنتی کار می‌کند

## سناریو ۱۵: مرز Federation سازمانی آینده

- با فرض اینکه:
	- احراز هویت سازمانی در یک Deployment آینده فعال شده است
- هرگاه:
	- اپراتور از مسیر هویت سازمانی وارد شود
- آنگاه:
	- مسیر مجاز آینده می‌تواند `Active Directory (AD/LDAP) -> Keycloak User Federation -> OIDC/OAuth2 -> Identity MCP / Gateway آینده MCP` باشد
	- MCP مستقیماً به AD/LDAP bind نمی‌شود
	- Keycloak/AD و Gateway مرکزی MCP اختیاری باقی می‌مانند و برای حالت Local/سرور مشتری لازم نیستند

## سناریو ۱۶: Manifest MCP و Validator استاندارد Corevia

- با فرض اینکه:
	- Manifest مربوط به Identity MCP در `.corevia/mcp/identity-service.yaml` وجود دارد
- هرگاه:
	- فرمان `corevia-run validate --pattern mcp-contract.validate --values .corevia/operations/identity-service-mcp-contract.values.yaml` اجرا شود
- آنگاه:
	- Corevia Standard pin‌شده، مرز Application، Modeها، Scopeها، Metadata Tool، Transport، Audit، Redaction، Rollback و نگاشت عمومی A01 تا A16 قبول می‌شوند
	- Pass شدن Manifest فقط Evidence قراردادی است و جای Implementation Runtime و تست‌های امنیتی را نمی‌گیرد

## شواهد اجرای Runtime مربوط به MCP

اولین Baseline اجرایی MCP Identity پیاده‌سازی و در محیط محلی تصدیق شده است:

- `IdentityService.Mcp` یک Sibling Adapter است و Application/MediatR را مستقیم فراخوانی می‌کند؛ کد MCP از API/Controller/HTTP Mirror، EF Core، Repository، SQL یا Provider خام استفاده نمی‌کند.
- Allowlist فعال دقیقاً ۳۶ Tool دارد: ۳۴ Tool نوع Atomic برای قابلیت‌ها و ۲ Tool نوع Business برای هدف‌های سطح‌بالا. پنج عملیات پروتکلی API به‌دلیل ممنوعیت افشای خام Token/Secret به‌صورت Bindingهای Inventory، Deprecated و غیرقابل‌Expose ثبت شده‌اند.
- Catalog برابری مجوز API/MCP شامل ۴۱ Mapping یک‌به‌یک است: ۳۶ مجوز فعال Authorization API به Tool فعال و قابل‌استفاده وصل‌اند و پنج عملیات پروتکلی ممنوع Binding صریح غیرقابل‌Expose دارند. Grantهای API و MCP جدا هستند و مجوز Authorization جدید API، Mapping/Tool یتیم یا Drift محدودیت‌ها در Validator استاندارد و CI به‌صورت Fail-closed شکست می‌خورد.

### Gateهای بستن رودمپ

- Acceptance مربوط به MCP Identity فقط با کد محلی، تست محلی یا Manifest معتبر بسته نمی‌شود. تغییرات Standards، Bridge و Market باید Commit و Push شوند، در Branchهای اعلام‌شده Merge شوند و CI ریموت سبز شود.
- فاز ۷ باید Evidence ریموت/Release و Smoke واقعی MCP را برای Transport محلی یا سرور مشتری ثبت کند. Pilot مشتری فاز ۸، Publish پکیج، Deploy تولید و تغییر Provider بیرونی Gateهای جدا و تأییدشده هستند.
- Executor قابل‌استفاده برای سرویس‌های آینده Follow-up ثبت‌شده است. تا زمان وجود Executor فرزند `apply` با Approval، Facade عمومی برای Mutation فقط خواندنی می‌ماند و نتیجه اجباری `extension-needed` است؛ Mutation دستی هرگز Evidence پذیرش نیست.
- سرور Context مربوط به Actor/Subject/Tenant را می‌سازد و Policyهای Self/Admin/Delegated، Scopeهای جدا، مرز Tenant، Delegation امضاشده و محدود، Nonce ضد Replay، Approval دقیق، Redaction و Audit را enforce می‌کند.
- پروژه MCP با صفر Warning و صفر Error Build شده و `IdentityService.Mcp.Tests` کل Suite مربوط به Catalog و Security را با موفقیت می‌گذراند.
- Smoke پروتکل stdio، `initialize` و `tools/list` را کامل می‌کند، دقیقاً ۳۶ Tool فعال منتشر می‌کند، stdout را فقط به پروتکل اختصاص می‌دهد و بدون Listener کستل اجرا می‌شود.
- Transport HTTP اختیاری، احراز هویت‌شده، محافظت‌شده با Host/Origin و پیش‌فرضاً Loopback-safe است و برای Bind غیر Loopback به HTTPS یا کنترل صریح شبکه خصوصی مورد اعتماد نیاز دارد؛ Smoke ایزوله پروفایل HTTPS/Private Network نیز موفق شد.
- اعتبارسنجی Remote CI/MR، Pilot سرور مشتری، انتشار Package، Deploy Production و کار آینده Gateway/Federation همچنان گیت Release هستند؛ این سناریوها فقط با شواهد محلی کامل تلقی نمی‌شوند.

دستورها و نتایج Redacted در [شواهد پیاده‌سازی Runtime MCP](./mcp-runtime-implementation-evidence.fa.md) ثبت شده‌اند.

## سناریوهای منفی
- نبودن کانفیگ Provider: استارت/دیپلوی reject می‌شود
- نبودن Secret الزامی: استارت fail می‌شود
- نبودن credential برای Provider دیتابیس انتخاب‌شده: استارت fail می‌شود
- خطای `401/403` Provider: مهاجرت تا اصلاح دسترسی blocked است
- Prefix/Path نامعتبر: مهاجرت تا اصلاح مسیرها blocked است
- شکست Discovery: مهاجرت تا سلامت رجیستری blocked است
- نقض ترتیب Bootstrap: مهاجرت blocked است

- MCP Controller یا endpoint HTTP را فراخوانی کند: اعتبارسنجی معماری fail می‌شود
- Actor/Subject فقط از Prompt یا metadata دلخواه گرفته شود: Authorization رد می‌شود
- Context delegation منقضی، بدون امضا، با audience اشتباه، بیش‌ازحد گسترده یا replay شده باشد: Authorization رد می‌شود
- Self Tool selector مربوط به کاربر/Tenant دیگر دریافت کند: Authorization رد می‌شود
- Mutation حساس MCP Approval دقیق نداشته باشد: اجرا block می‌شود
- HTTP محلی MCP روی interface عمومی bind یا با forwarding بدون احراز هویت expose شود: اعتبارسنجی Deployment fail می‌شود
- MCP Token/Secret/OTP خام expose کند: اعتبارسنجی Tool و Security fail می‌شود

## مشاهده‌پذیری
- لاگ‌های لازم از `Logging` مشترک باید در دسترس باشند
- شکل خطاها از `ErrorHandling` مشترک باید حفظ شود

## گیت نهایی بازبینی
- پاکسازی فایل‌های محلی: بله
- نگهداری فقط fallback غیرحساس در `appsettings.json`: بله
- اعتبارسنجی credential واقعی Provider: بله
- اعتبارسنجی تزریق در `IConfiguration`: بله
- موفقیت Smoke Test وابسته به config/secret: بله
- اعتبارسنجی Discovery و `/health`: بله
- اعتبارسنجی `ServiceID/CheckID` روی runtime host: بله

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
