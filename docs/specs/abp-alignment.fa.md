[English](./abp-alignment.md)

# استانداردهای ABP: اسپک استخراج‌شده و فاصلهٔ DotNetArch از آن

هدف: قراردادهای منتشرشدهٔ ABP Framework به‌عنوان استاندارد مرجع گرفته شود، فاصلهٔ DotNetArch (layout v2 و سرویس‌های Corevia) با آن سنجیده شود و آنچه پذیرفته شده ثبت شود. وضعیت: **پذیرفته‌شده توسط مالک در 2026-10-09** (تصمیم‌های D-25..D-29)؛ برنامهٔ پیاده‌سازی فاز ۱۲ roadmap است.

## 1. منابع
همهٔ قواعد از مستندات رسمی ABP ‏(نسخهٔ latest، بررسی‌شده 2026-10-09) خوانده شد:
[معماری ماژول](https://abp.io/docs/latest/framework/architecture/best-practices/module-architecture)، [Entity](https://abp.io/docs/latest/framework/architecture/best-practices/entities)، [Repository](https://abp.io/docs/latest/framework/architecture/best-practices/repositories)، [Domain Service](https://abp.io/docs/latest/framework/architecture/best-practices/domain-services)، [Application Service](https://abp.io/docs/latest/framework/architecture/best-practices/application-services)، [DTO](https://abp.io/docs/latest/framework/architecture/best-practices/data-transfer-objects)، [یکپارچگی EF Core](https://abp.io/docs/latest/framework/architecture/best-practices/entity-framework-core-integration)، [ساختار solution لایه‌ای](https://abp.io/docs/latest/solution-templates/layered-web-application/solution-structure)، [ساختار solution میکروسرویس](https://abp.io/docs/latest/solution-templates/microservice/solution-structure).

## 2. استانداردهای ABP
### 2.1 Solution و پکیج‌ها
- نام‌گذاری `CompanyName.ModuleName.<Layer>`؛ یک solution برای هر ماژول؛ هر پکیج وابستگی‌هایش را اعلام می‌کند.
- ریشه: ‏`src/`، `test/` ‏(مفرد)، `etc/` ‏(`docker/`، `helm/`، `scripts/`، `abp-studio/`)، `common.props`، `NuGet.Config`.
- پکیج‌ها و ارجاع‌های مجاز:

| پکیج | محتوا | ارجاع |
|---|---|---|
| `Domain.Shared` | ثابت‌ها، enumها، انواع قابل اشتراک (بدون entity) | هیچ |
| `Domain` | entity، value object، رابط repository، domain service | Domain.Shared |
| `Application.Contracts` | رابط‌های `I...AppService` و DTOها | Domain.Shared |
| `Application` | پیاده‌سازی application serviceها | Domain، Application.Contracts |
| `EntityFrameworkCore` / `MongoDB` | DbContext و پیاده‌سازی repository (قابل تعویض) | فقط Domain |
| `HttpApi` | controllerهای REST، یکی برای هر application service | فقط Application.Contracts |
| `HttpApi.Client` | پراکسی‌های کلاینت راه‌دور | فقط Application.Contracts |
| `Web` / `HttpApi.Host` / `DbMigrator` | میزبان‌ها و ابزارها | HttpApi، Application، EF |

- الگوهای استفاده: مونولیت (Web + Application + EF)، میکروسرویس (HttpApi + Application + EF)، UI + راه‌دور (Web + HttpApi.Client)، مصرف‌کنندهٔ کلاینت (HttpApi.Client)، پراکسی API ‏(HttpApi + HttpApi.Client).
- تست‌ها زیر `test/`: ‏`Domain.Tests`، `Application.Tests`، `EntityFrameworkCore.Tests`، `Web.Tests`، `TestBase`.
- قالب میکروسرویس: مونوریپوی `apps/`، `gateways/`، `services/`، `etc/`؛ سرویس ساده ۲ پروژه است (میزبان + `.Contracts`) و سرویس با منطق کسب‌وکار واقعی یک ماژول لایه‌ای است که یک `HttpApi.Host` کوچک میزبانی‌اش می‌کند.

### 2.2 قواعد هر حوزه
- Entity: ‏aggregate root با یک id از نوع `Guid` که از بیرون داده می‌شود (هرگز داخل ساخته نمی‌شود)، aggregate کوچک، constructor اصلی public/protected که اعتبارسنجی می‌کند، constructor بدون پارامتر protected برای ORM، اعضای virtual، setter خصوصی با متدهای محافظت‌شده، ارجاع به aggregateهای دیگر فقط با id.
- Repository: ‏رابط در Domain، فقط برای aggregate rootها، نام اختصاصی (نه `IRepository<T>` عمومی در کد application)، همهٔ متدها async با `cancellationToken` اختیاری، پرچم‌های `includeDetails`، بدون افشای `IQueryable`.
- Domain Service: پسوند `Manager`، بدون رابط مگر لازم، بدون getter، متدهای تغییر وضعیت با نام بیانگر قصد، ‏`BusinessException` با کدهای خطای namespace‌دار، هرگز DTO برنمی‌گرداند، بدون منطق کاربر جاری.
- Application Service: یکی برای هر aggregate root، رابط در Contracts با پسوند `AppService`، متدهای `Async` با نام‌های `GetAsync`، `GetListAsync`، `CreateAsync`، `UpdateAsync(id, dto)`، `DeleteAsync`، هرگز entity نمی‌گیرد یا برنمی‌گرداند، DTO ورودی جدا برای هر متد، data annotation برای اعتبارسنجی، بدون LINQ در سرویس (از repository استفاده شود)، هرگز application serviceهای همان ماژول را صدا نمی‌زند، بدون انواع وب مثل `IFormFile`.
- DTO: در Contracts، serializable، ‏get/set عمومی، attributeهای اعتبارسنجی، بدون منطق.
- EF Core: رابط و کلاس DbContext ‏(`AbpDbContext<T>`)، ثابت‌های `TablePrefix`/`Schema`، نگاشت با متدهای افزونهٔ `Configure[Module]` و `ConfigureByConvention`، ‏repository از `EfCoreRepository` ارث می‌برد، متدهای افزونهٔ `IncludeDetails`.

## 3. فاصلهٔ DotNetArch (layout v2) از ABP
| حوزه | ABP | ‏DotNetArch v2 | فاصله |
|---|---|---|---|
| لایه‌ها | ۸ تا ۱۰ پکیج از جمله Domain.Shared، Application.Contracts، HttpApi، HttpApi.Client | ‏Domain، Application، Infrastructure، Api ‏(+ Mcp، Contracts اختیاری، `Infrastructure.<Provider>` اختیاری) | زیاد: بدون Domain.Shared، بدون جداسازی Contracts/Application، بدون جداسازی HttpApi/Host، بدون پکیج کلاینت |
| پوشه‌ها | ‏`src/`، `test/`، `etc/` | ‏`src/`، `tests/` (جمع)، بدون `etc/` | کم (تغییر نام و افزودن `etc/`) |
| جهت وابستگی | ‏HttpApi فقط به Contracts | ‏Api به Application و Infrastructure ارجاع می‌دهد (composition root) | سبک متفاوت، هر دو رو به داخل |
| سبک application | یک application service برای هر aggregate | ‏CQRS با MediatR، یک use case در هر پوشه | پارادایم متفاوت |
| Entity | ‏id از نوع Guid، ctor محافظت‌شده، اعضای virtual | ‏setter خصوصی و رفتار؛ نوع id مشخص نیست | متوسط |
| Repository | برای هر aggregate root، async، ‏`includeDetails`، بدون `IQueryable` | ‏repository async بدون `IQueryable`؛ ‏`IRepository<T>` عمومی + unit of work | متوسط |
| Domain Service | پسوند `Manager`، ‏`BusinessException` | تولید نمی‌شود | شکاف |
| DTO | در Contracts | در `Features/<Plural>/Dtos` داخل Application | متوسط |
| EF Core | رابط DbContext، prefix/schema، ‏`Configure[Module]` | ‏EF Core در Infrastructure، بدون رابط یا prefix | متوسط |
| تست‌ها | ‏`test/` با تست Domain، Application، EF | تست هر لایه زیر `tests/` ‏(+ تست Domain با `fix`) | کم |
| فریم‌ورک زمان اجرا (ماژول، چندمستأجری، audit، permission، localization، settings) | داخلی | در ابزار نیست (متعلق به Kitها) | خارج از دامنه |
| مستندات | مستندات ماژول | ‏spec، roadmap، تصمیم و evidence دوزبانه | ‏DotNetArch جلوتر است |

## 4. فاصلهٔ سرویس‌های Corevia
روی Catalog بعد از جابه‌جایی به layout v2 سنجیده شد: ‏`src/` و پروژه‌های جدای `Contracts`/provider را دارد؛ ندارد: ‏Domain.Shared، Application.Contracts برای سرویس‌ها، پکیج‌های HttpApi/Client و `etc/`؛ از handlerهای CQRS استفاده می‌کند نه application service؛ idها business id هستند (نه `Guid`)؛ persistence ‏EF Core نیست. پس سرویس‌های Corevia در «پوشه‌های شبیه ABP، درونی متفاوت» هستند.

## 5. تصمیم‌ها (مالک، 2026-10-09)
۱. **ABP مرجع ساختاری ابزار است** (D-25). فریم‌ورک زمان اجرای ABP (ماژول، چندمستأجری، audit، permission، localization، settings) کپی نمی‌شود و در Kitها می‌ماند.
۲. **layout v3 = پوشه‌های هم‌شکل ABP**: ‏`src/`، `test/` (مفرد)، `etc/` (D-26). v2 ‏(`tests/`) برای سرویس‌هایی که قبلاً جذب شده‌اند معتبر می‌ماند؛ ‏`fix --rules=DA-A01` از v2 به v3 می‌برد.
۳. **پروژه‌های لایه** ‏`Domain.Shared`، `Application.Contracts`، `HttpApi`، `HttpApi.Client` برای ابزار اختیاری و برای پروفایل الزامی‌اند (D-27). **لایه فقط وقتی وجود دارد که چیزی برایش هست**: ‏`fix --rules=DA-A02` پروژهٔ لایه را فقط همراه فایل‌هایی می‌سازد که به آن می‌روند (فایل‌های فقط-enum به `Domain.Shared`؛ DTO و requestهای MediatR به `Application.Contracts`؛ controllerها به `HttpApi`)، با namespace و مسیر داخل پروژهٔ ثابت، پس کدی ویرایش نمی‌شود. آدم پشت CLI می‌تواند با `--empty` لایهٔ خالی بخواهد (برنامهٔ توسعه دست خودش است)؛ agent روی MCP پیش‌فرض را می‌گیرد: آنچه مربوط است مهاجرت بده یا کاری نکن.
۴. **مجموعه‌قاعدهٔ داخلی و اختیاری `abp` در `doctor`** (D-28) با شناسه‌های `DA-A01..DA-A07`، با `standards: [abp]` در `project.yml` یا در پروفایل فعال می‌شود؛ پروفایل می‌تواند شدت هر شناسهٔ قاعده را هم بالا ببرد (`severity:` در پروفایل)، پس سازمان می‌تواند پروژه‌های لایه را الزامی کند در حالی که ابزار فقط هشدار می‌دهد. فقط قواعد ساختاری دارد؛ قاعده‌ای دربارهٔ idهای `Guid` یا application service ندارد، پس سرویس‌های CQRS با business id استثنا نمی‌خواهند.
۵. **سرویس مالک چیزی است که منتشر می‌کند** (D-29): فایل‌هایی که سرویس برای سامانه‌های دیگر منتشر می‌کند (مثلاً اعلان مسیر gateway) داخل خود سرویس زیر `etc/` است و مصرف‌کننده جمعشان می‌کند؛ سرویس هرگز به مصرف‌کننده وابسته نیست. اینکه کدام فایل و کدام مصرف‌کننده، دانش سطح سازمان است و به پروفایل تعلق دارد.

## 6. قواعد حاصل
| شناسه | قاعده | نوع |
|---|---|---|
| DA-A01 | layout v3: پروژه‌ها زیر `src/` و پروژه‌های تست زیر `test/`؛ ‏`etc/` جای فایل‌های غیرکدی است که سرویس منتشر می‌کند و اختیاری است | ساختار |
| DA-A02 | هر جا فایلی به لایه‌ای تعلق دارد (فایل فقط-enum، DTO و request، controller) پروژهٔ آن لایه وجود دارد | ساختار |
| DA-A07 | پروژهٔ لایه‌ای که هیچ فایل سورسی ندارد نباید وجود داشته باشد | ساختار |
| DA-A03 | جهت ارجاع: Domain.Shared هیچ؛ Domain به Domain.Shared؛ Contracts به Domain.Shared؛ HttpApi و HttpApi.Client فقط به Contracts (پروژهٔ persistence می‌تواند به Application ارجاع دهد چون portها آنجاست) | ساختار |
| DA-A04 | رابط‌های repository: متد async، `CancellationToken` اختیاری در آخر، بدون بازگرداندن `IQueryable` | کد |
| DA-A05 | سرویس‌های دامنه به `Manager` ختم می‌شوند؛ رابط application service به `AppService` ختم می‌شود و در Contracts است (فقط وقتی چنین نوعی هست) | کد |
| DA-A06 | نوع‌های DTO در `Application.Contracts` هستند (فقط وقتی آن پروژه هست) | کد |

## 7. مسیر fix و مهاجرت
- `fix --rules=DA-A01`: از v2 به v3 (layout flat اول DA-S06) (`tests/` به `test/` با بازنویسی همهٔ مسیرهای اشاره‌کننده، با همان ساز‌وکار DA-S06).
- `fix --rules=DA-A02`: سورس را تحلیل می‌کند، پروژه‌های لایه‌ای را که چیزی برای دریافت دارند می‌سازد، فایل‌ها را جابه‌جا می‌کند (namespace و مسیر داخل پروژه ثابت)، ارجاع‌ها را می‌افزاید (Domain به Domain.Shared، Application به Application.Contracts، میزبان به HttpApi)، پکیج‌ها و framework reference و `InternalsVisibleTo` موردنیاز فایل‌های جابه‌جاشده را کپی می‌کند، پروژه‌های جدید را در مرحلهٔ restore در Dockerfile فهرست می‌کند و در solution ثبت می‌کند.
- تحلیل نوع‌های داده‌ای مستقلِ موردنیاز فایل جابه‌جاشده را هم می‌آورد (recordها، enumها، مدل‌های HTTP در پوشه‌های `Contracts/`، `Models/`، `Dtos/`) و هر فایلی را که وابستگی‌هایش نمی‌توانند با آن بروند نگه می‌دارد؛ هر مورد با فایل مانع به‌عنوان دستی فهرست می‌شود. controllerها با هم می‌روند: یک controller مسدود همه را نگه می‌دارد، چون شکستن میزبان بین دو assembly کشف route را خراب می‌کند.
- تحلیل وابستگی متنی است (نام نوع‌ها) و کامپایلر نیست: نامی که به‌عنوان نوع استفاده شود وابستگی است، نام عضو و پارامتر نیست. بنابراین بعد از گام مهاجرت باید build و تست اجرا شود؛ سرویسی که رد شود برگردانده می‌شود، وصله نمی‌شود.
