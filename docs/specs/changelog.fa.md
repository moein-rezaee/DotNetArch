# تغییرات

## 2.0.0 (منتشرنشده)
- mapperهای entity در `Application/Features/<Plural>/Mappings` می‌مانند؛ تشخیص DTO بر اساس نوع دادهٔ تعریف‌شده است، نه متدهای extension مثل `ToDto` (‏R-I4، ‏AC-29).
- میزبان‌های Api تولیدشده نام enum را می‌پذیرند (R-K16، ‏AC-68).
- بخش پروفایل `database` (R-K15): Kit ‏DatabaseConnection جای کد اتصال خود قالب را می‌گیرد.
- چندفرهنگی و سلامت به‌صورت پیش‌فرض (R-K12، ‏AC-65): میزبان‌های Api تولیدشده اول فرهنگ درخواست را تعیین می‌کنند (en، fa)، عنوان خطا را از `Resources/SharedMessages*.resx` می‌خوانند و `/health/live` و `/health/ready` را نگاشت می‌کنند.
- ‏`--export-openapi [file]` روی میزبان‌های Api تولیدشده (R-K13، ‏AC-66) snapshot ‏OpenAPI را بدون سرور می‌نویسد.
- ‏`migrate_framework` خانوادهٔ بسته‌های runtime را روی یک نسخه می‌برد، runtime نصب‌شدهٔ هدف یا `runtime_version` (R-K14، ‏AC-67).
- ‏`swap_kit` (R-K11، ‏AC-64): یک حوزهٔ kit را در همان جا با kitهای یک profile مشترک عوض می‌کند (بسته‌ها، خط ثبت، using). ۴۵ عملیات.
- ‏`migrate_framework` (R-K10، ‏AC-63): مخزن را به نسخهٔ .NET که ورودی است می‌برد (frameworkها، پین SDK، تگ imageها، بسته‌های همراه runtime)؛ بسته‌های شخص ثالث وابسته به runtime فهرست می‌شوند. ۴۴ عملیات.
- آداپتر همسایه (R-K9): پروژهٔ `<App>.Link.<Peer>.<Transport>` (‏Rest و InProc و Event) مثل `<App>.Infrastructure.<System>` آداپتر است؛ ‏`add_adapter --peer=true` آن را می‌سازد و قاعده‌ها و `move_code` و `split_registration` و `add_composition` و `describe_module` آن را می‌یابند. ‏`fix` برای DA-AD05 و DA-AD08 خط‌های بسته را داخل PropertyGroup یک‌خطی می‌نویسد.
- ‏DA-AD09 و `add_adapter` (R-K9، ‏AC-62): HTTP خروجی از Infrastructure مشترک به پروژهٔ آداپتری می‌رود که ابزار می‌سازد.
- فایل‌های migration از editorconfig پیروی می‌کنند (R-J22، ‏AC-53): byte order markی که EF می‌نویسد برداشته می‌شود تا کار lint روی سرویس تازه پاس شود.
- ‏`add migration --name=Initial` (R-J23، ‏AC-69) با رجیستری مشترک CLI و MCP یک migration ‏EF می‌سازد و پایگاه‌داده را تغییر نمی‌دهد.
- چندمستاجری روی سیستم مرجع بیرونی (R-J21، ‏AC-52): ‏`add_tenancy --wire_only` فقط kit چندمستاجری را وصل می‌کند.
- ساخت Kit (R-J20، AC-51): ‏`scaffold_kit` و `scaffold_provider` ناحیه‌های Kit و بسته‌های ارائه‌دهنده را با تست، مستندات دوزبانه و ورودی فهرست بسته‌ها می‌سازند؛ ‏`fix --rules=DA-A08` کنترلرهای پنهان از توصیف API (‏`IgnoreApi = true`) را رد می‌کند. ۳۵ عملیات، CLI و MCP از یک registry. برای ساخت Kitهای Delivery و Chat استفاده شد.
- دروازه‌های مستندات و نسخه (R-J19 و AC-50): doctor ‏DA-M10 (لینک زبان یک جفت) و DA-B10 (`<Version>` پروژهٔ Api) با fixer؛ قالب‌های README و docs لینک‌ها، بخش‌های استاندارد README و نسخه را دارند.
- سرویس‌های قدیمی (R-J15 تا R-J18 و AC-46 تا AC-49): ‏`dotnet-arch.yml` از دیسک خوانده می‌شود (DA-G01)، ‏`add_persistence` (اسکلت استاندارد persistence برای سرویس بدون پایگاه داده)، ‏`add_tenancy` (چندمستاجری روی profile مشترک؛ doctor ‏DA-TN01 انتخابی)، fix برای DA-K04 و DA-M09 بدون میزبان تست کامل، میزبان MCP دست‌نویس دست نمی‌خورد، و plan محتوا را مقایسه می‌کند؛ ۳۳ عملیات. اولین استفاده روی Payment، Notification و Basket.
- ‏`fix --rules=DA-D03` مرحلهٔ نهایی Dockerfile مبتنی بر image اجرای .NET را با کاربر غیر root اجرا می‌کند (R-J14 و AC-45).
- ورودی‌های typed طول اعتبارسنجی می‌گیرند (‏`Name:string(8000)`) و controller featureی که از قبل در لایهٔ HttpApi است دوباره در پروژهٔ Api اعلام نمی‌شود (R-J8، AC-39). سرویس لایسنس (`corevia-licensing`) با نمونهٔ `Dm.LicenseGenerator` اولین سرویسی بود که به هر دو نیاز داشت.
- ‏`fix --rules=DA-BP01` برای سرویس بدون blueprint آن را استخراج می‌کند (R-J13 و AC-44).
- ‏doctor و fix: کلاینت typed، GET با رکورد query را بیان می‌کند، ‏`.env` نادیده‌گرفته‌شده یافته نیست، ‏DA-M09 با تست snapshot قرارداد OpenAPI اصلاح می‌شود و blueprint طول‌های ثبت‌شده را برمی‌گرداند (R-J12 و AC-43).
- ‏blueprintها نسخه دارند (‏`schema`، ‏`version` و `.net-arch/service-blueprint.lock`)؛ doctor آن‌ها را می‌سنجد (‏DA-BP01..05) و `fix` اعمالشان می‌کند (R-J11 و AC-42).
- پورت repository تولیدشده تازه‌ترین را اول صفحه‌بندی می‌کند و می‌شمارد (‏`newestFirst` و `CountAsync`؛ R-J10 و AC-41).
- ‏`apply_blueprint` (تولید آنچه blueprint سرویس توصیف می‌کند، در یک طرح و تکرارپذیر، R-J9 و AC-40)؛ ۳۱ عملیات. طول ورودی رشته‌ای typed در `dotnet-arch.yml` ثبت می‌شود و use case می‌تواند DTO entity دیگری را برگرداند.
- ‏`apply_profile` (wire یا ارتقای kitهای خارجی پروفایل در solution موجود، R-J7)؛ ۳۰ عملیات. مقدار نمونهٔ رشته‌ها طول اعلام‌شده را رعایت می‌کند. تست‌های تولیدشده: تست Api برای مسیر ناشناخته 404 یا 401 را می‌پذیرد (hostی با سیاست fallback پاسخ 401 می‌دهد) و تست DI در Infrastructure دیگر کل گراف را هنگام build اعتبارسنجی نمی‌کند (سرویس‌های kit و host را Api ثبت می‌کند).
- پرچم `~` روی propertyهای `new entity` آن را از DTO بیرون نگه می‌دارد؛ use caseهای تایپ‌دار که نام یک enum را می‌آورند namespace ‏enum را می‌گیرند.
- ‏Dockerfile تولیدشده دیگر با خط `# syntax=` شروع نمی‌شود (frontend پیش‌فرض BuildKit برای `RUN --mount` کافی است؛ آن خط به image ‏frontend نیاز دارد که registry آفلاین یا خصوصی ممکن است نداشته باشد).
- ‏`new entity` (propertyهای تایپ‌دار و persistence بدون CRUD)، `new feature` (گروه use case بدون entity)، `new enum --members`، متدهای repository ‏`FirstOrDefaultAsync` / `ListAsync`، ‏`DomainException.OptionalText` (R-J5، R-J6)؛ ۲۹ عملیات.
- ‏`external_kits` پروفایل و `new solution --profile`: kitهای منتشرشده از روی داده در hostهای Api و MCP و CLI wire می‌شوند و در صورت اعلام kit، کد دستی Swagger و exception-handler برداشته می‌شود (R-J4، D-39).
- ‏`new solution --in-place`؛ use caseهای تایپ‌دار (‏`new action --input --output --anonymous --description` که به شکل `usecase.*` در `dotnet-arch.yml` ثبت می‌شود)؛ ‏`add_cli` و `new solution --cli`: میزبان CLI هم‌سطح میزبان MCP با تست برابری (R-J1..R-J3، D-36..D-38)؛ ۲۷ عملیات. رفع شد: متغیر بلااستفاده‌ای که build حالت Release ابزار را می‌شکست.
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
