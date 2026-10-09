[English](./abp-alignment.md)

# استانداردهای ABP: اسپک استخراج‌شده و فاصلهٔ DotNetArch از آن

هدف: قراردادهای منتشرشدهٔ ABP Framework به‌عنوان استاندارد مرجع گرفته شود، فاصلهٔ DotNetArch (layout v2 و سرویس‌های Corevia) با آن سنجیده شود و تصمیم گرفته شود چه چیزی همین حالا بیاید. این سند چیزی را در ابزار تغییر نمی‌دهد؛ تصمیم‌ها در پایان فهرست شده‌اند.

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

## 5. پیشنهاد
قراردادهای ساختاری و ارزان ABP را بپذیرید و بخش‌های وابسته به فریم‌ورک زمان اجرای ABP را بیرون نگه دارید:
1. نام‌های layout قابل‌تنظیم شود و پیش‌فرض‌ها با ABP هم‌تراز شوند: ‏`src/`، `test/`، `etc/`.
2. یک blueprint لایه‌ای اختیاری (`--layout=layered`) با `Domain.Shared`، `Application.Contracts`، `HttpApi`، `HttpApi.Client` و یک میزبان نازک اضافه شود، چون کلاینت تایپ‌دار و Contractهای مشترک همان چیزی است که سرویس‌های Corevia امروز ندارند؛ سرویس‌های موجود روی v2 بمانند.
3. قواعد نام‌گذاری و repository در ABP به‌صورت پروفایل اختیاری به `doctor` اضافه شود (async + cancellation token، بدون `IQueryable`، پسوند `Manager`، پسوند `AppService` برای رابط، DTO در Contracts) تا جذب قابل‌اندازه‌گیری باشد.
4. سیستم ماژول زمان اجرا، چندمستأجری و localization در ابزار کپی نشود؛ در Kitها بماند.
5. ‏idهای Guid و application service به سرویس‌هایی که business id و CQRS دارند تحمیل نشود: به‌عنوان استثنای پذیرفته‌شدهٔ پروفایل Corevia ثبت شود.

## 6. تصمیم‌های لازم
- نام پوشه `test` ‏(ABP) در برابر `tests` ‏(فعلی): تغییر آن یک قاعدهٔ ابزار و یک جابه‌جایی برای هر سرویس جذب‌شده است (Catalog در 2026-10-09 به `tests/` رفت).
- اینکه blueprint لایه‌ای بخشی از چرخهٔ بعد باشد یا فاز جدا در roadmap.
- اینکه قواعد ABP پروفایل پیش‌فرض ابزار شوند یا اختیاری بمانند.
