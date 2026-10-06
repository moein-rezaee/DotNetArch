# تغییرات مشخصات IdentityService

## 0.11.17 (1405/07/13)

- راه‌حل موقت checkout هم‌ردیف Kit حذف شد: مصرف `Corevia.Kit.*` اکنون منحصراً از طریق `PackageReference`های منتشرشده و بازیابی‌شده از Feed NuGet در Nexus (`NuGet.config`) انجام می‌شود. هر `ProjectReference` به `Corevia.Kit.*` از طریق `$(CoreviaKitPackagesRoot)` در `IdentityService.Api`، `IdentityService.Application`، `IdentityService.Infrastructure`، `IdentityService.Infrastructure.Database` و `IdentityService.Mcp` با `PackageReference` نسخه‌دار معادل جایگزین شد: `Corevia.Kit.ErrorHandling.{Core,Abstractions}` نسخه 2.0.0، `Corevia.Kit.DatabaseConnection.{Core,Providers.Postgres,Providers.SqlServer}` نسخه 2.0.0 و `Corevia.Kit.DatabaseConnection.Abstractions` نسخه 1.0.0 (تازه منتشرشده)، در کنار `Corevia.Kit.Logging.{Core,Providers.Console}`، `Corevia.Kit.Swagger.Core`، `Corevia.Kit.JwtSecurity.Core`، `Corevia.Kit.Config.Providers.Consul`، `Corevia.Kit.Secrets.Providers.Vault`، `Corevia.Kit.ConfigLoader.Core` و `Corevia.Kit.ServiceDiscovery.Providers.Consul`، `Corevia.Kit.Http.Core` که از قبل روی نسخه‌های ۱.۰.x منتشر شده بودند. فایل `Directory.Build.props` (که فقط ویژگی موقت `CoreviaKitPackagesRoot` را نگه می‌داشت) حذف شد. `IdentityService.Api/Dockerfile` و `docker-compose.yml` دیگر به named/additional build context با نام `corevia-kit` در BuildKit نیاز ندارند. ورودی `kit-not-on-nexus` در `.corevia/operations/migration-followups.yaml` حذف شد (رفع‌شده)؛ ورودی `ci-job-token-kit-read` باقی ماند (مربوط به checkout کیت در `.gitlab-ci.yml` مدیریت‌شده، در ادامه ببینید). هیچ تغییری در رفتار اپلیکیشن، کلید پیکربندی یا قرارداد عمومی رخ نداد؛ `dotnet build`/`dotnet test` بدون تغییر ماندند (۲۳۶ تست، همه موفق).
- `.gitlab-ci.yml` توسط `service-cicd.split` مدیریت می‌شود و عمداً دست‌نخورده باقی ماند: همچنان بلوک `service_cicd.kit_stopgap` را اجرا می‌کند (checkout یک ref پین‌شده از `corevia-kit` و ارسال آن به‌عنوان build context در BuildKit) با وجود اینکه اکنون همه `.csproj`ها Kit را از Nexus بازیابی می‌کنند. این رفتار تکراری اما بی‌خطر است، چون Dockerfile به‌سادگی context بلااستفاده را نادیده می‌گیرد. پیگیری باقی‌مانده (در اینجا انجام نشده، نیازمند تغییر در مخزن standards خارج از این مخزن است): تنظیم `service_cicd.kit_stopgap.enabled: false` در `corevia-standards/patterns/service-cicd.split/values.corevia-identity.yaml` و اجرای `corevia-run apply --approve` برای بازسازی `.gitlab-ci.yml` بدون checkout/راه‌حل موقت Kit.
- نسخه runtime پکیج `1.0.53`.

## 0.11.16 (1405/07/13)

- اصلاحات گیت پس از مهاجرت (`service-migration.validate`): مقدار پیش‌فرض ثابت `POSTGRES_PASSWORD` (`options.Postgres.PasswordDefault = "postgres"`) از `IdentityService.Infrastructure.Database` حذف شد؛ Providerهای Kit پوشش داده شدند تا نبودن یا خالی بودن `POSTGRES_PASSWORD` با `InvalidOperationException` و نام همان کلید شکست بخورد و `SQLSERVER_PASSWORD` فقط وقتی `SQLSERVER_USER` تنظیم شده الزامی باشد (SQL Server بدون نام کاربری و رمز مطابق رفتار اصلی از Integrated Security استفاده می‌کند). سایر مقادیر پیش‌فرض و کلیدهای پیکربندی بدون تغییر ماندند؛ `.env.example` اکنون هر دو کلید رمز را به‌صورت placeholder دارد.
- Readiness واقعی: `/health` (liveness) همان پاسخ سبک را نگه می‌دارد؛ مسیر جدید `/health/ready` با health checkهای ASP.NET Core و بررسی اتصال پایگاه داده با EF Core (timeout سه ثانیه) اجرا می‌شود و در نبود پایگاه داده `503` برمی‌گرداند. `IdentityService.Mcp` (Host از نوع HTTP) همین دو مسیر را ناشناس ارائه می‌دهد. Kit قابلیت health ندارد (پیگیری ثبت شد).
- قراردادهای نمونه: `appsettings.example.json` در Api اکنون `Swagger:Documents` را مانند `appsettings.json` به‌صورت آرایه دارد؛ `IdentityService.Mcp/appsettings.example.json` اضافه شد؛ `.env.example` کلیدهای `COREVIA_MCP_BEARER_TOKEN`، `COREVIA_MCP_APPROVAL` و `COREVIA_MCP_DELEGATION` را (به‌صورت placeholder) گرفت.
- گیت مستمر: `.corevia/operations/service-migration.values.yaml`، ورودی `service-migration` در `.corevia/validators.yaml` و `.corevia/operations/migration-followups.yaml` (مالکان برای Kit در Nexus، توکن job در CI، تست‌های یکپارچگی DbInitializer، شکاف‌های Kit، گراف ساختار سورس و اثبات build داکر)؛ گزارش استخراج اکنون همه فایل‌های حذف/تغییرنام/تغییریافته را توضیح می‌دهد.
- تست‌ها: ۲۳۶ تست در solution (۱۰ تست بیشتر از ۲۲۶: شکست نبود رمز (Postgres؛ کاربر SQL Server بدون رمز)، Integrated Security در SQL Server، readiness با ۲۰۰/۵۰۳ و liveness). نسخه runtime پکیج `1.0.52`.

## 0.11.15 (1405/07/13)

- رعایت لایه‌بندی Clean Architecture نسبت به Kit نسخه 2.0.0: `IdentityService.Application` اکنون فقط به `Corevia.Kit.ErrorHandling.Abstractions` وابسته است (exceptionها به namespace ‏`Corevia.Kit.ErrorHandling.Abstractions.Exceptions` منتقل شدند؛ بدون وابستگی به `ErrorHandling.Core`/ASP.NET، به‌علاوه رفرنس صریح `Microsoft.Extensions.Logging.Abstractions`). `Infrastructure` و `Infrastructure.Database` هم از `ErrorHandling.Abstractions` استفاده می‌کنند؛ فقط `Api` و `Mcp` (composition root) برای middleware به `ErrorHandling.Core` وابسته می‌مانند.
- `IdentityService.Infrastructure.Database` صراحتاً به Abstractions، Core و هر دو Provider پکیج DatabaseConnection رفرنس می‌دهد و آن‌ها را (`AddCoreviaPostgresProvider().AddCoreviaSqlServerProvider()`) پیش از `AddCoreviaDatabase<IdentityDbContext>` ثبت می‌کند؛ انتخاب موتور همچنان با پیکربندی است و همه‌ی کلیدها و مقادیر پیش‌فرض (`IdentityDb:Provider`/`DB_PROVIDER`، `POSTGRES_*`، `SQLSERVER_*`) بدون تغییر ماندند.
- ۲ تست اضافه شد (گزارش نبودن registration provider به‌ازای هر موتور)؛ مجموع ۲۲۶ تست. نسخه runtime پکیج `1.0.51`.

## 0.11.14 (1405/07/12)

- مرحله 9: ممیزی و تکمیل پوشش تست (`connectcore service-tests`): پروژه‌های `IdentityService.Application.Tests` (115)، `IdentityService.Infrastructure.Tests` (63) و `IdentityService.Api.Tests` (19) در کنار `IdentityService.Mcp.Tests` (27) اضافه شدند؛ جمعاً ۲۲۴ تست در solution و مرحله test در CI. ایرادهای کشف‌شده و اصلاح‌شده: فیلتر phone در `GetUsersPaged` از overload ای از `Contains(string, StringComparison)` استفاده می‌کرد که EF قادر به ترجمه آن نیست (هر فیلتر phone خطا می‌داد)؛ `OtpRestClient.VerifyCodeAsync` برای پاسخ 2xx غیر‌بولین `JsonException` خام نشت می‌داد (اکنون `ExternalServiceException` با bad gateway)؛ `DbInitializer` اکنون خطاهای اتصال به provider را به `ExternalServiceException` با کد `database_unavailable` (503) تبدیل می‌کند. Values در `.corevia/operations/service-tests.values.yaml`.
- مرحله 8: تفکیک CI/CD با `service-cicd.split` (فایل `.gitlab-ci.yml` فقط-CI: build، test، lint، security-scan، package؛ بدون stage استقرار و بدون stage پاکسازی؛ قالب CD در `.corevia/cicd/deploy.values.example.yaml` برای `service-artifact.deploy`). Pipeline نسخه pin‌شده corevia-kit (`COREVIA_KIT_REF`) را checkout می‌کند و `CoreviaKitPackagesRoot` را تنظیم می‌کند. `IdentityService.Api/Dockerfile` اصلاح شد تا از ریشه همین مخزن build شود و Kit را به‌صورت named context در BuildKit با نام `corevia-kit` بگیرد (`docker buildx build --build-context corevia-kit=../corevia-kit`؛ `docker-compose.yml` از `additional_contexts` استفاده می‌کند). پیگیری ثبت‌شده: انتشار Corevia.Kit.* در Nexus، تبدیل به `PackageReference` و حذف checkout کیت، named context و `CoreviaKitPackagesRoot`. دو اصلاح whitespace باعث موفقیت job مربوط به lint شد.
- مرحله 7: Transport پیش‌فرض MCP از `stdio` به `http` تغییر کرد (stdio فقط برای توسعه و خارج از Development رد می‌شود) تا با پروفایل استاندارد هم‌خوان باشد؛ `mcp-contract.validate` و `mcp-permission-parity.validate` موفق هستند.
- مرحله 5b: جداسازی agent-context (`repo-agent-context.create-or-update`، بلاک managed به‌علاوه راهنمای Identity در `AGENTS.md`؛ رودمپ و decision log مربوط به MCP منتقل شد). مقدار پیش‌فرض hardcode شده `POSTGRES_PASSWORD` از `IdentityDbContextFactory` (زمان طراحی) حذف شد و اکنون در نبود متغیر خطای واضح می‌دهد؛ سایر پیش‌فرض‌ها بدون تغییر هستند.
- مرحله 5a از Phase C (جداسازی Specs). فایل‌های `docs/specs/openspec.yaml` و `testspec.yaml` از طریق `repo-specs.create-or-update` (`corevia-run apply`) بازتولید و با نقشه قراردادهای HTTP/MCP/own-storage مخصوص Identity تکمیل شدند. Manifest مربوط به MCP (`.corevia/mcp/identity-service.yaml`) و مقادیر contract/parity/source از monorepo منتقل و به این repo اشاره داده شدند؛ لینک‌های نسبیِ monorepo در `docs/specs/*` و `README*.md` اصلاح شدند.
- مراحل ۲ و ۳ فاز C: استخراج از مونورپو corevia-market (snapshot از `develop` با کامیت 63cb7d92، فایل `docs/evidence/monorepo-extraction-file-diff.md`) و اصلاح کامل Kit drift. ارجاع‌های قدیمی `shared/*` با `Corevia.Kit.*` جایگزین شدند (راه‌حل موقت: `ProjectReference` به checkout هم‌ردیف `corevia-kit` از طریق `Directory.Build.props`؛ انتشار روی Nexus پیگیری ثبت‌شده است). ارجاع بلااستفاده `shared/CacheService` و `DotNetEnv` حذف شدند. `OtpHttpClient` به `OtpRestClient` روی `Corevia.Kit.Http` تبدیل شد؛ `IdentityDbContextFactory` از `Corevia.Kit.DatabaseConnection` استفاده می‌کند؛ `McpAuthorizationException` از `ServiceException` Kit ارث می‌برد؛ introspection در OIDC از پارامترهای اعتبارسنجی JwtBearer ثبت‌شده توسط Kit استفاده می‌کند. کلیدهای تنظیمات، endpointها و فرمت توکن‌ها تغییری نکرده‌اند.
- استثناهای پذیرفته‌شده (فایل `.corevia/operations/kit-drift.values.yaml`): اعتبارسنجی توکن stdio در MCP (JwtSecurity در Kit اعتبارسنج مستقل ندارد) و اتصال مستقیم Provider در `DbInitializer` (ساخت دیتابیس خارج از دامنه Kit DatabaseConnection است).
- نکته ترتیب Startup: `AddConfigLoaderExtension` در Kit سرویس‌های ثبت‌شده قبلی را کپی می‌کند؛ بنابراین Providerهای Config/Secrets بلافاصله قبل از آن ثبت می‌شوند؛ میزبان stdio در MCP از یک shim مستند استفاده می‌کند چون Kit overload برای `IHostApplicationBuilder` ندارد.

## 0.11.13 (1405/07/12)

- نسخه runtime سرویس Identity برای ساخت با پروژه یکپارچه دیتابیس به `1.0.50` افزایش یافت.

## 0.11.12 (1405/07/12)

- پروژه‌های `IdentityService.Infrastructure.Postgres`/`.Infrastructure.SqlServer` در یک پروژه‌ی واحد `IdentityService.Infrastructure.Database` ادغام شدند، طبق تصمیم `Corevia.Kit.DatabaseConnection` ثبت‌شده در `corevia-kit/docs/specs/market-shared-documentation-migration-audit.md`. انتخاب Provider دیتابیس، ساخت Connection String و ثبت `UseNpgsql`/`UseSqlServer` در EF Core اکنون به نقطه‌ی ورود واحد `AddCoreviaDatabase<TContext>` از `Corevia.Kit.DatabaseConnection` واگذار شده‌اند به‌جای پیاده‌سازی دستی جدا برای هر Engine؛ متد `AddIdentityInfrastructure` در `IdentityService.Infrastructure` دیگر خودش بر اساس Engine شاخه‌بندی نمی‌کند. تمام کلیدهای تنظیمات موجود (`IdentityDb:Provider`، `DB_PROVIDER`، `POSTGRES_*`، `SQLSERVER_*`، `Database:Postgres:*`، `Database:SqlServer:*`، `IdentityDb:CommandTimeoutSeconds`) و مقادیر پیش‌فرض آن‌ها دقیقاً حفظ شدند. `Corevia.sln` و `IdentityService.Api/Dockerfile` برای ارجاع به پروژه‌ی جدید به‌روزرسانی شدند؛ هیچ تغییری در API/قرارداد عمومی رخ نداد.
- نسخه runtime سرویس Identity به `1.0.49` افزایش یافت.

## 0.11.11 (1405/07/08)

- سازگاری دیتابیس Identity موجود روی SQL Server مشتری اصلاح شد: پس از `EnsureCreatedAsync` ستون افزایشی `RefreshTokens.ReplacedByTokenId` به‌صورت idempotent ایجاد می‌شود تا خطای schema در introspection و احراز هویت ایونت سرویس‌ها رخ ندهد.
- نسخه runtime سرویس Identity به `1.0.48` افزایش یافت.

## 0.11.10 (2026-09-27)
- baseline غیرحساس کلاینت M2M با شناسه `woosync-service` پس از حذف سرویس WooSync از مونوریپو برگردانده شد؛ provider مستقل ووکامرس در `corevia-sync` همچنان به همین client id و `M2M_WOOSYNC_SERVICE_SECRET` از Vault نیاز دارد.

## 0.11.9 (2026-09-22)
- کلاینت M2M مربوط به `woosync-service` و ورودی‌های seed اسکوپ `woosync.read`/`woosync.write` از `appsettings.json`/`appsettings.example.json` و `IdentitySeedService.cs` حذف شد، و `M2M_WOOSYNC_SERVICE_SECRET` از `contracts.md` و چک‌لیست resolve مربوط به M2M در `acceptance.md` حذف شد، چون `WooSyncService` کاملاً از مونوریپو حذف و به‌عنوان provider ووکامرس به ریپوی `corevia-sync` منتقل شد. `DidarSyncService` دست‌نخورده باقی موند ولی الان صراحتاً منسوخ‌شده (deprecated) علامت‌گذاری شده (جایگزین آن provider دیدار در `corevia-sync` است)؛ کلاینت M2M و اسکوپ‌های `didarsync-service` تا حذف نهایی در یک مرحله‌ی جداگانه‌ی بعدی دست‌نخورده باقی می‌مانند.

## 0.11.8 (2026-09-21)
- کلاینت M2M مربوط به `rubikabot-service` و اسکوپ `rubikabot.send` که به `catalog-service` داده شده بود از `appsettings.json`/`appsettings.example.json` حذف شد، ورودی seed اسکوپ `rubikabot.send` از `IdentitySeedService.cs` حذف شد، `M2M_RUBIKABOT_SERVICE_SECRET` از `.env` و `contracts.md` حذف شد، چون `RubikaBotService` کاملاً از مونوریپو حذف شده (فاز B1 [رودمپ مادر جداسازی Market](https://gitlab.getcorevia.ir/corevia/market/-/blob/develop/docs/roadmaps/services/market-decomposition-master-roadmap.fa.md)).
- نسخه runtime سرویس Identity به `1.0.46` افزایش یافت.

## 0.11.7 (2026-09-18)
- یک outage تولید حیاتی که تمام لاگین‌ها را می‌شکست رفع شد: migration دستی `AddRefreshTokenReplacedByTokenId` (اضافه‌شده در `0.11.4`) فاقد attributeهای `[DbContext]`/`[Migration]` بود که `EF Core`'s `MigrateAsync()` برای شناسایی و اجرای یک migration بهشون نیاز داره. بدون این attributeها migration به‌صورت بی‌صدا نادیده گرفته می‌شه — نه خطایی، نه warning‌ای — و لاگ startup هم `"✅ Database migrations applied successfully"` رو نشون می‌ده انگار همه‌چی موفق بوده. نتیجه: ستون `RefreshTokens.ReplacedByTokenId` هیچ‌وقت در Postgres پروداکشن ساخته نشد، و هر تلاش برای ورود (`POST /v1/api/Identity/Verify`) با `500 server_error` fail می‌شد چون آخرین `SaveChangesAsync` در `VerifyCommandHandler` سعی می‌کرد یک ردیف `RefreshToken` با یک ستون ناموجود insert کنه.
- این باگ با بررسی مستقیم لاگ‌های زنده کانتینر روی سرور پروداکشن (`docker logs identity-service`) پیدا شد که یک `Npgsql.PostgresException: 42703: column "ReplacedByTokenId" of relation "RefreshTokens" does not exist` رو نشون می‌داد.
- attributeهای `[DbContext(typeof(IdentityDbContext))]` و `[Migration("20260918083443_AddRefreshTokenReplacedByTokenId")]` به کلاس migration اضافه شد تا `EF Core` بتونه به‌درستی تشخیصش بده و روی دیپلوی بعدی اعمالش کنه.
- **الگوی خطرناک مشابه پیدا شد ولی رفع نشد** (خارج از scope این hotfix): دو migration دستی قبلی (`StandardizeOrderWriteScopeFormat`, `MakeCustomerRefreshTokensNonExpiring`) هم همین نقص attribute رو دارن. تاثیر `MakeCustomerRefreshTokensNonExpiring` احتمالاً با یک workaround جداگانه با SQL خام در `DbInitializer.EnsureRefreshTokenExpiryIsNullableAsync` پوشش داده شده (که به نظر می‌رسه دقیقاً به همین دلیل قبلاً اضافه شده بوده). این الگو باید در یک تسک جدا بررسی و برای همه‌ی migrationهای دستی فعلی و آینده رفع بشه.
- نسخه runtime سرویس Identity به `1.0.45` افزایش یافت.

## 0.11.6 (2026-09-18)
- علت اصلی واقعیِ یک شکست پایدار در build سرویس CI رفع شد که دوبار به اشتباه به‌عنوان مشکل شبکه/روتینگ Nexus تشخیص داده شده بود: لایه‌ی `COPY` اولیه‌ی `IdentityService.Api/Dockerfile` (که برای مرحله‌ی cache-friendly `dotnet restore` استفاده میشه) هیچ‌وقت `IdentityService.Infrastructure.SqlServer.csproj` رو کپی نمی‌کرد، پس اون restore هیچ‌وقت `ProjectReference` مربوط به `IdentityService.Infrastructure.SqlServer` رو نمی‌دید و هیچ‌وقت `Microsoft.EntityFrameworkCore.SqlServer` رو نمی‌گرفت. مرحله‌ی بعدی `dotnet publish` (بعد از `COPY . .` که کل درخت سورس رو میاره) مجبور می‌شد اون یک پکیج رو برای اولین بار به‌صورت زنده restore کنه — ولی `--configfile` رو پاس نمی‌داد، پس بی‌صدا به `NuGet.config` ساده و بدون credential برمی‌گشت، که یه خطای ۴۰۱ تکرارپذیر دقیقاً برای همون یک پکیج تولید می‌کرد، صرف‌نظر از URL restore.
- خط `COPY` گمشده برای `IdentityService.Infrastructure.SqlServer.csproj` اضافه شد و `--mount=type=secret` + `--configfile` هم به مرحله‌ی `dotnet publish` اضافه شد تا اگه هنوز نیاز به restore زنده داشته باشه، دقیقاً مثل مرحله‌ی restore قبلی احراز هویت بشه.
- نسخه runtime/application سرویس Identity به `1.0.44` افزایش یافت.

## 0.11.5 (2026-09-18)
- یک مقدار پیش‌فرض اشتباه که مستقیم commit شده بود رفع شد: `IdentityDb:Provider` در `appsettings.json` مقدار `"SqlServer"` داشت، درحالی‌که هم قرارداد مستندشده (پیش‌فرض باید Postgres باشه تا رفتار پروداکشن حفظ بشه) و هم fallback کد سی‌شارپ (`"postgres"`) هر دو Postgres رو می‌خوان. این مقدار اشتباه به‌صورت ناخواسته توسط یه کامیت بزرگ و نامرتبط در ژوئن ۲۰۲۶ وارد شده بود؛ چون Consul در پروداکشن به‌صورت خاموش این مقدار رو override می‌کرد، کسی متوجه نشده بود. به `"Postgres"` برگردونده شد.
- یک لاگ تشخیصی در زمان startup به `AddIdentityInfrastructure` اضافه شد که مشخص می‌کنه کدوم provider دیتابیس واقعاً انتخاب شده و از کدوم منبع کانفیگ — تا این دسته از ناهماهنگی از این به بعد توی لاگ‌های کانتینر قابل مشاهده باشه.
- نسخه runtime/application سرویس Identity به `1.0.43` افزایش یافت.

## 0.11.4 (2026-09-18)
- رفع باگ خروج ناخواسته از حساب: rotation توکن refresh در `RefreshCommandHandler` تک‌مصرف بود و هیچ بازه زمانی (grace window) نداشت، پس دو فراخوانی refresh هم‌زمان برای یک session (خیلی معمول موقع رفرش صفحه — اجرای دوباره افکت‌ها، چند تب باز، یا retry بعد از یک درخواست کند) با هم رقابت می‌کردن. درخواستی که در این رقابت می‌بازد یک توکن از‌قبل‌باطل‌شده ارائه می‌ده و با خطای سخت `invalid_refresh_token` رد میشه، که فرانت‌اند اون رو به‌عنوان خروج کامل از حساب تفسیر می‌کنه، درحالی‌که session هنوز معتبره.
- فیلد `RefreshToken.ReplacedByTokenId` اضافه شد که موقع rotation یک توکن مقداردهی میشه. اگه یک توکن باطل‌شده در بازه ۱۰ ثانیه‌ای grace دوباره ارائه بشه و جایگزینش هنوز معتبر باشه، حالا handler همون جایگزین رو (با توکن دسترسی جدید و همون توکن refresh معتبر) برمی‌گردونه به‌جای رد کردن درخواست. استفاده مجدد بیرون از بازه grace، یا بدون جایگزین معتبر، همچنان مثل قبل رد میشه.
- migration جدید `AddRefreshTokenReplacedByTokenId` اضافه شد.
- نسخه runtime سرویس Identity به `1.0.42` افزایش یافت.

## 0.7.7 (2026-07-06)

- کلیدهای secret موردنیاز seed برای Invoice/POS به‌صورت صریح اعلام شدند و نبود secret یا scope مالی اجباری باعث توقف startup می‌شود.
- نسخه runtime سرویس Identity به `1.0.31` افزایش یافت.

## 0.7.6 (2026-07-06)

- scopeهای `pos.payment.start` و `invoice.payment.confirm` اضافه شدند.
- متادیتای کلاینت‌های M2M پیش‌فرض `invoice-service` و `pos-service` برای جریان امن POS اضافه شد.
- کلید `M2M_POS_SERVICE_SECRET` به قرارداد secretهای runtime اضافه شد.
- نسخه runtime سرویس Identity به `1.0.30` افزایش یافت.

[English](./changelog.md) | [فارسی](./changelog.fa.md)

## 0.7.5 (2026-06-12)

## ۰.۱۱.۱ (2026-09-03)
- endpoint ارسال OTP حالا بعد از پذیرش ارسال پس‌زمینه، پاسخ صریح `202 Accepted` با وضعیت `accepted/queued` برمی‌گرداند.
- قرارداد Identity و کلاینت فرانت‌اند با ارسال غیرهمزمان OTP هماهنگ شد و نسخه Runtime مربوط به Identity/MCP به `1.0.39` افزایش یافت.

## ۰.۱۱.۰ (2026-08-29)

- اولین Baseline کامل برابری مجوزهای API/MCP در Identity تکمیل شد: ۴۱ ورودی Catalog، ۳۶ Tool فعال Authorization (۳۴ Atomic و ۲ Business) و پنج Binding صریح غیرقابل‌Expose برای عملیات پروتکلی.
- Validation به‌صورت Fail-closed برای Permission جدید API، Mapping مفقود، Tool یتیم، Tool غیرقابل‌استفاده، Drift محدودیت‌ها، جداسازی صریح Grant MCP و Classification ایمنی Token/Secret خام اضافه شد.
- Acceptance، Overview، Roadmap، Runtime Evidence و Flow Verification دوزبانه با Catalog کامل و Contractهای قابل‌استفاده مجدد Corevia Standard/Bridge همگام شدند.
- Gateهای باقی‌مانده برای بستن صریح شدند: Commit/Push/MR/CI سه Repository، Smoke واقعی MCP فاز ۷، Pilot/Release مشتری فاز ۸ و Executor قابل‌استفاده مجدد آینده.
- نسخه Patch Runtime مربوط به Identity/MCP به `1.0.38` افزایش یافت.

## ۰.۱۰.۰ (2026-08-28)

- اولین Runtime اجرایی `IdentityService.Mcp` به‌صورت Sibling روی Application/MediatR پیاده‌سازی شد؛ بدون HTTP Mirror یا دسترسی مستقیم به داده.
- Catalog اولیه Toolهای Atomic/Business، Authorization سمت سرور برای Self/Admin/Delegated، مرز Tenant، Approval دقیق عملیات حساس، خروجی Redacted و پوشش Audit اضافه شد.
- پشتیبانی Transport با Generic Host برای stdio و HTTP کنترل‌شده اضافه شد؛ شامل جداسازی stdout از Protocol، ایمنی Loopback و کنترل صریح شبکه برای سرور مشتری.
- شواهد پیاده‌سازی Runtime، Contract/Acceptance/Overview دوزبانه و Job CI برای Build، Test و Boundary مربوط به MCP Identity اضافه شد.
- بررسی عضویت Subject محول‌شده در Tenant اضافه و نسخه Release مربوط به Identity/MCP به `1.0.37` همگام شد.
- اعتبارسنجی Remote CI/MR، Pilot مشتری، انتشار، Deploy Production، Gateway مرکزی و Federation سازمانی به‌عنوان گیت‌های باز صریح باقی ماندند.

## ۰.۹.۴ (2026-08-28)

- نسخه Patch سرویس IdentityService برای عبور اصلاحات Manifest و حاکمیت MCP از change policy مخزن به 1.0.36 افزایش یافت.

## ۰.۹.۳ (2026-08-28)

- گزارش اعتبارسنجی Facade MCP فقط برای Identity، به‌همراه گیت‌های مورد انتظار Approval و Apply read-only، ثبت شد.
- اصلاح و بررسی Source مربوط به Entry Pointهای واقعی Query/Command در Manifest مستند شد.

## ۰.۹.۲ (2026-08-28)

- نگاشت Entry Pointهای Application در Manifest مربوط به MCP Identity به قراردادهای واقعی Query/Command اصلاح و وجود هر مسیر اعلام‌شده در سورس تأیید شد.

## ۰.۹.۱ (2026-08-28)

- قرارداد Values سطح سرویس `mcp-service.create-or-migrate` به‌عنوان نقطه ورود واحد کار MCP در Identity اضافه شد.
- Skill تولیدشده Corevia ثبت شد و کشف Source در Identity بدون Skill دستی و بدون Gateway اجباری به Pattern فرزند ایجاد یا مهاجرت هدایت می‌شود.
- Evidence قطعی برای هر دو شاخه Source جدید و Source موجود اضافه و اعتبارسنجی شد.

## ۰.۹.۰ (2026-08-13)

- مستندات IdentityService از استاندارد قابل استفاده مجدد MCP Corevia مصرف می‌کنند و قواعد عمومی فقط در مستندات Identity نگه‌داری نمی‌شوند.
- Manifest ماشین‌خوان Identity MCP و قرارداد Values مربوط به Validator read-only به نام `mcp-contract.validate` اضافه شد.
- سناریوهای پذیرش اختصاصی Identity به Baseline عمومی A01 تا A16 استاندارد Corevia map شدند.
- Scopeهای اختصاصی Identity، Audienceهای Agent مدیریتی داخلی/مشتری Delegated، Mappingهای Application، Allowlist MVP، Profile مربوط به Redaction و Profile استقرار در مستندات سرویس باقی ماندند.

## ۰.۸.۰ (2026-08-13)

- قرارداد معماری MCP به‌صورت sibling و deployment-neutral در کنار `IdentityService.Api` اضافه شد.
- قواعد صریح Self، Admin، Delegated، Security، Actor/Subject، مرز Tenant، Approval، Audit و Redaction برای MCP اضافه شد.
- مدل ترکیبی Atomic/Business Tool و ممنوعیت Toolهای عمومی API/SQL/Repository ثبت شد.
- قراردادهای استقرار Local با `stdio`/loopback و استقرار روی سرور مشتری با HTTPS، به‌همراه مرز استفاده Cloud AI از MCP محلی، اضافه شد.
- مسیر `Active Directory (AD/LDAP) -> Keycloak User Federation -> OIDC/OAuth2` و Corevia MCP Gateway مرکزی به‌عنوان integration اختیاری آینده و نه dependency فعلی runtime ثبت شد.

## ۰.۷.۵ (2026-08-05)

- assembly مربوط به migrationهای EF به‌صورت صریح تنظیم شد و nullable بودن `RefreshTokens.ExpiresAt` برای schemaهای قدیمی در startup، قبل از اجرای migration، به‌صورت idempotent اصلاح می‌شود.
- نسخه IdentityService به `1.0.35` افزایش یافت.

## 0.7.4 (2026-07-01)

- پشتیبانی آداپتر Provider دیتابیس برای IdentityService اضافه شد: Postgres همچنان Provider پیش‌فرض است و SQL Server بدون تغییر در منطق هویت از طریق `IdentityDb:Provider` یا `DB_PROVIDER` قابل انتخاب است.
- metadata نسخه runtime سرویس Identity به `1.0.28` افزایش یافت.

## 0.7.4 (2026-06-03)

- scope `order.write` به scopeهای پیش‌فرض کلاینت M2M با شناسه `didarsync-service` اضافه شد تا DidarSyncService بتواند سفارش‌های دیدار را از طریق endpoint احرازشده create-order در OrderService dispatch کند.
- metadata نسخه runtime سرویس Identity به `1.0.27` افزایش یافت.

## 0.7.3 (2026-05-30)

- پوشش seed احراز هویت DidarSync اضافه شد: `didarsync.read`، `didarsync.write`، permissionهای عملیاتی مرتبط، و متادیتای پیش‌فرض کلاینت M2M با شناسه `didarsync-service`.
- کلید Secret با نام `M2M_DIDARSYNC_SERVICE_SECRET` به قرارداد runtime سرویس Identity اضافه شد.

## 0.7.2 (2026-04-14)

- reference صریح پروژه API به `ServiceDiscoveryExtension` اضافه شد.
- شواهد migration runtime با readهای واقعی Consul، Vault و Swagger از سرور به‌روزرسانی شد.
- کلیدهای متمرکز `ServiceDiscovery:Services:Otp` در Consul اضافه و verify شدند.
- سکشن بلااستفاده `Logging` از `IdentityService.Api/appsettings.json` حذف شد چون در اکستنشن فعلی logging مصرف نمی‌شد.
- شناسه‌های عملیاتی discovery سرویس Identity نرمال شد:
  - `ServiceID=nikplus-prd-online-shop-identity-service-5270`
  - `CheckID=service:nikplus-prd-online-shop-identity-service-5270`

## 0.7.1 (2026-04-11)

- قرارداد نام‌گذاری scopeهای Identity در مسیر M2M به‌صورت نقطه‌ای یکپارچه شد.
- seed و پیکربندی پیش‌فرض کلاینت‌ها با `order.write` همگام شد (جایگزین `order:write`).
- خروجی `/introspect` برای access token با فیلدهای `scope` و `client_id` تکمیل شد و اعتبارسنجی اختیاری client credentials نیز اضافه شد.

## 0.7.0 (2026-04-09)

- مهاجرت Runtime سرویس به Providerهای متمرکز انجام شد (کانفیگ در Consul و Secret در Vault) و Merge در Startup با `ConfigLoaderExtension` انجام می‌شود.
- قرارداد ترتیب Startup در `Program.cs` اعمال شد (`AddConfigLoaderExtension(...)` قبل از Options Binding و ثبت dependencyهای وابسته به کانفیگ).
- مقادیر حساس hardcoded از `.env` حذف و فایل به مدل bootstrap-only تبدیل شد.
- قرارداد Deploy در CI به مدل شش متغیر مشترک تغییر کرد و suffix سرویس (`.../identity-service`) در Job مشتق‌سازی می‌شود.
- گیت‌های عملیاتی برای خوانایی Provider و سلامت Discovery به قرارداد پذیرش اضافه شد.
- متادیتای کامل `IdentityClient:M2MClients` شامل `Name`، `Description` و `Scopes` به Consul (مسیر `identity-client/m2m-clients/*`) منتقل شد.
- مقادیر fallback غیرحساس در `appsettings.json` برای پایداری startup در اختلال موقت Providerها حفظ شد.

## 0.6.1 (2026-04-05)

- فایل `IdentityService/docker-compose.yml` به‌روزرسانی شد تا شبکه‌های اشتراکی `services` و `data` را با `external: true` مصرف کند.
- خطای دیپلوی ناشی از وجود شبکه‌های از پیش‌ساخته روی سرور (بدون labelهای compose) برطرف شد.

## 0.6.0 (2026-04-05)

## 0.5.0 (2026-04-05)

- اسکوپ M2M جدید `rubikabot.send` برای یکپارچگی RubikaBotService اضافه شد.
- scopeهای کلاینت M2M مربوط به `catalog-service` با `rubikabot.send` به‌روزرسانی شد.

## 0.4.0 (2026-02-24)

- مقدار شماره Bootstrap برای `SuperAdmin` در `IDENTITY_ROOT_PHONE` به `09150501658` تغییر کرد.
- پرمیشن‌های `Catalog.Product.ByCode.Get` و `Catalog.Product.WithoutImageExcel.Get` به Seed اضافه شدند.
- اسکوپ `catalog.read` به پرمیشن‌های جدید کاتالوگ map شد.

## 0.3.0 (2026-02-17)

- baseline عمومی با محتوای مبتنی بر منطق واقعی سرویس جایگزین شد.
- توضیحات صریح برای مسئولیت‌ها، وابستگی‌ها و سناریوهای منفی اضافه شد.

## 0.2.0 (2026-02-17)

- baseline اولیه فایل‌های `overview`، `contracts` و `acceptance` تکمیل شد.

## 0.1.0

- ایجاد baseline اولیه مشخصات سرویس.

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
