# تغییرات

## 2.0.0 (منتشرنشده)
- ‏DA-A13 (هشدار doctor در مجموعهٔ `abp` و fixer ‏`fix --rules=DA-A13`): هر پوشهٔ پروژه مستقیم زیر `src/` (یا ریشهٔ تست) با نام دقیق پروژه‌اش است؛ پوشه‌های تودرتو (`src/api/App.Api`، `src/Extensions/App/App.Core`) و حروف نادرست جابه‌جا می‌شوند و ارجاع‌های پروژه (دوباره محاسبه‌شده)، solution، Dockerfileها (از جمله `COPY --from=build /src/src/...`)، Compose، CI، اسکریپت‌ها و راهنماهای Markdown هماهنگ می‌شوند. پروژه‌های لایهٔ تازه `LangVersion` پین‌شدهٔ مخزن را می‌گیرند؛ تست لایه برای پروژهٔ .NET Framework اخطار نسخهٔ cross-framework (‏MSB3277/NU1701) را خاموش می‌کند و اگر مخزن نسخه‌های مرکزی دارد و پکیج‌های تست را نداشته، برایشان نسخهٔ مرکزی می‌گذارد.
- یافته‌های مهاجرت نه سرویس .NET: fixer لایه پکیج‌هایی را که فایل‌های جابه‌جاشده با namespace نام می‌برند (مثل `Microsoft.EntityFrameworkCore` و `Corevia.Kit.*`) به پروژهٔ لایهٔ تازه می‌برد؛ صدا زدن متد extension وابستگی حساب می‌شود (controllerی که یکی را صدا بزند می‌ماند)؛ کامنت بین attributeها و action دیگر action را از کلاینت typed پنهان نمی‌کند؛ پکیج transport پروژهٔ کلاینت نسخهٔ Kit را می‌گیرد (مرکزی یا مستقیم)؛ doctor پوشه‌هایی را که نمی‌تواند بخواند رد می‌کند.
- **شکستنده** (D-32..D-34): مولدها عملیات رجیستری‌اند. هر دستور تغییردهنده اول طرح می‌دهد و فقط با `--apply` می‌نویسد (MCP: ‏`apply: true`)؛ رفتار فوری قبلی در 1.3.x است. ابزارهای MCP مقدارهای رجیستری (`path`، `entity`، ...) را می‌گیرند نه نام‌های camelCase قبلی (`solutionPath`، ...). ‏`new solution` پیش‌فرض layout v3 (ABP) دارد؛ ‏`--layout=v2|legacy` می‌ماند.
- یک رجیستری برای همهٔ دستورها: کلمه‌های CLI (`new crud`) و ابزار MCP (`new_crud`) از آن مشتق می‌شوند؛ ۲۶ عملیات. تازه: ‏`add_layer`، `add_tests`، `spec_list`، `spec_add`، `spec_check`، `graph`؛ ‏`adopt --standards=abp`.
- در پروژهٔ ABP بعد از هر مولد fixerهای ABP (DA-A01، A02، A10، A11، A08، A09) و تازه‌سازی `.net-arch/` اجرا می‌شود؛ solution تولیدشده با entity، action و event بدون هشدار build می‌شود، تست‌هایش پاس است و یافتهٔ ABP ندارد.
- قالب‌ها: DTO داده‌ی خالص است، نگاشت entity فایل جدا دارد، پورت repository ‏async است (`RemoveAsync`) و `IQueryable` برنمی‌گرداند؛ ریشهٔ تست‌ها از layout پیروی می‌کند (`tests` یا `test`).
- کلاینت تایپ‌دار همراه تست‌هایش تولید می‌شود و از routeهای بعدی controller پیروی می‌کند (R-I5)؛ فریم‌ورک پروژه‌های تولیدی، وقتی پروژه‌ها نام نبرند، از Directory.Build.props می‌آید.
- ‏`folder_moves` در پروفایل و `fix --rules=DA-A12` (D-35)؛ ‏DA-A09 اشاره‌های Compose به مخزن دیگر را دست نمی‌زند.
- doctor: ‏DA-A10 و DA-A11؛ بررسی `IQueryable` کامنت‌ها را نادیده می‌گیرد.

## منتشرنشده (در 2.0.0 ادغام می‌شود)
- دستور جدید `dotnet-arch doctor` (تشخیص فقط‌خواندنی: لایه‌ها، جهت وابستگی، تست و پوشش، بهداشت build، پیکربندی و secretها، Docker/CI، مستندات، قواعد کد، مرز kitها؛ خروجی متن یا `--json`؛ کد خروج ۳ برای حالت مسدودکننده).
- دستور جدید `dotnet-arch adopt` ‏(پروژهٔ موجود را فقط با نوشتن `.net-arch/` تحت کنترل می‌آورد) و `dotnet-arch fix` ‏(اصلاحات مکانیکی بهداشتی، اول طرح، با `--apply` می‌نویسد).
- پوشهٔ جدید `.net-arch/` ‏(`project.yml`، `rules.yml`، `profile.yml`، `generated.lock`) با `~/.net-arch/` سراسری؛ rules.yml تغییر شدت، آستانه‌ها و استثنا با دلیل را نگه می‌دارد.
- قواعد سازمانی یک پروفایل اعلانی است (`profile.yml`)؛ خود ابزار هیچ بررسی مخصوص سازمان ندارد.
- یک رجیستری عملیات هم CLI و هم سرور MCP را تغذیه می‌کند (۳ ابزار رجیستری: `doctor`، `adopt`، `fix`؛ در مجموع ۱۸ ابزار).
- قاعده‌های ساختاری اختیاری `fix`: ‏DA-B03 نسخه‌های مرکزی پکیج، DA-B07 هشدار به‌عنوان خطا، DA-S04 پروژهٔ تست Domain، DA-S06 جابه‌جایی به layout v2 ‏(پروژه‌ها به `src/`، تست‌ها به `tests/`، بازنویسی هر مسیر اشاره‌کننده)؛ اصلاح پیش‌فرض `.editorconfig` اکنون یک نشانگر بدون قاعده است تا lint روی کد موجود پاس بماند.
- doctor: اعتبارسنجی مستندات (فهرست، specها، snapshot ‏OpenAPI) و بررسی تست (وجود تست، پوشش در برابر آستانه).
- هم‌ترازی با ABP پذیرفته شد (D-25..D-29، `abp-alignment.fa.md`): ‏layout v3 با `src/test/etc`، پروژه‌های لایهٔ اختیاری، مجموعه‌قاعدهٔ اختیاری `abp` در `doctor`، قاعده‌های `fix` ‏DA-A01 و DA-A02 مبتنی بر محتوا (فایل‌های متعلق به `Domain.Shared`، `Application.Contracts`، `HttpApi` را جابه‌جا و ارجاع‌ها/پکیج‌ها/Dockerfile را وصل می‌کند؛ لایهٔ خالی فقط با `--empty`) و `fix` ‏DA-A08 (کلاینت HTTP تایپ‌دار از روی controllerها، روی `HttpClient` یا روی انتزاع REST client که پروفایل نام می‌برد) و DA-A09 (فایل‌های Compose به `etc/docker/`) و DA-A10 (درخت پوشه‌ها داخل لایه‌ها، D-31) و DA-A11 (ارجاع‌های پروژه، solution، restore در Dockerfile) و `new solution --layout=v3` (پیاده‌سازی در فاز ۱۲ roadmap).

## 1.3.0
- ابزار به `DotNetArch.Core` (پیاده‌سازی)، `DotNetArch.Cli` (دستورها) و `DotNetArch.Mcp` (سرور MCP) با تست هر لایه بازساخت شد؛ اجرای فرایند بدون shell؛ اعتبارسنجی نام‌ها.
- قالب جدید میکروسرویس layout v2: Domain/Application/Infrastructure/Api، vertical slice با CQRS ‏(MediatR)، unit of work و repository، مدیریت مرکزی پکیج، بارگذاری پیکربندی، تست هر لایه.
- `new service` سرویس Application یا Kit مستقل می‌سازد؛ recipeهای Cache و MessageBroker و MediaStorage و حوزهٔ عمومی؛ `new kit` و `add kit`.
- Docker، Git (hostهای شخصی)، CI برای GitHub/GitLab/Azure/Bitbucket/Gitea، registry شخصی Docker و NuGet؛ `ci add`، `docker add`، `git setup`.
- MCP: `dotnet-arch mcp serve` با ۱۵ ابزار؛ host ‏MCP تولیدشده (`--mcp`، `add mcp`).
- پروژه‌ها و kitهای تولیدشده AGENTS.md، spec، roadmap و دفتر تصمیم را به دو زبان دارند.
- رفع: crash اسپینر بدون ترمینال و برداشته شدن `samples/` در build ابزار.

## 1.2.0
- نسخهٔ منتشرشدهٔ قبلی (تاریخچهٔ git).
