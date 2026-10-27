# DotNetArch - نیازمندی‌ها (مرجع اصلی)

نسخهٔ انگلیسی: `requirements.md`. شناسه‌ها یکسان‌اند. هر چیزی که مالک گفته با شناسهٔ ثابت اینجا ثبت شده و حذف یا ضعیف کردنش فقط با ثبت در `docs/decisions/decisions.fa.md` مجاز است.

## الف. میکروسرویس تولیدشده
- R-A1. Clean Architecture با لایه‌های جدا (پروژهٔ جدا).
- R-A2. Ports و Adapters: پورت‌ها (interface) مال Application/Domain، آداپتورها مال Infrastructure/Api.
- R-A3. ترکیب Vertical Slice و CQRS و Unit of Work و Repository.
- R-A4. Query و Command و Action به‌صورت تودرتو داخل پوشهٔ همان entity.
- R-A5. CQRS با MediatR مثل sample.
- R-A6. رعایت Clean Architecture و Clean Code و SOLID.
- R-A7. wiring درست: هر لایه وابستگی‌های خودش را با DI extension خودش ثبت می‌کند.
- R-A8. پکیج‌های NuGet هم همین‌طور: هر لایه فقط پکیج‌های لازمِ خودش را دارد.
- R-A9. پیکربندی: environment برای secret و مقادیر runtime، appsettings برای مقادیر غیرحساس؛ هر دو هنگام load پروژه در یک `IConfiguration` بارگذاری می‌شوند.
- R-A10. تولید `.env.example` و `appsettings.example.json` و همگام نگه داشتن با کد.
- R-A11. پشتیبانی Docker.
- R-A12. پشتیبانی Git (init، gitignore، commit اولیه، تنظیم remote/host).
- R-A13. پشتیبانی MCP در پروژهٔ تولیدشده: tools مطابق controller و service ها بدون تکرار منطق.
- R-A14. پشتیبانی تست: فقط برای پروژهٔ تولیدشده (نه خود ابزار).
- R-A15. پشتیبانی CI: خود ابزار گزینهٔ مناسب را انتخاب کند؛ git شخصی/self-hosted؛ registry شخصی برای Docker و NuGet.

## ب. `new service`
- R-B1. حالت داخلی (دارای logic) همان رفتار فعلی (درست است).
- R-B2. حالت خارجی (بدون logic): رفتار فعلی اشتباه است و با تولید Kit جایگزین می‌شود.
- R-B3. Kit شامل Abstractions و Core و N پکیج Provider است (کم و زیاد می‌شود).
- R-B4. نام Kit بر اساس قابلیت است نه محصول: Cache، MessageBroker، MediaStorage. نه Redis و نه RabbitMQ.
- R-B5. الگوی kit همان `samples/MediaStorage` است.
- R-B6. Kitها کاملاً مستقل‌اند: نسخه‌بندی جدا، build در CI، انتشار در NuGet registry شخصی.
- R-B7. همهٔ Kitها در یک پوشه (`kits/`) و دسته‌بندی بر اساس حوزه.
- R-B8. افزودن kit به سرویس: Abstractions در Application، و Core + Providers فقط در composition root.

## ج. خود ابزار به‌عنوان MCP
- R-C1. حالت MCP برای استفادهٔ agent از کتابخانه/ابزار (ساخت solution، entity، crud، action، event، enum، constant، service/kit). جدا از R-A13.

## د. مستندات
- R-D1. مستندات کامل ابزار، پروژهٔ تولیدشده و kit.
- R-D2. Spec-driven: specها در ابزار، پروژه و kit؛ تغییر رفتار = تغییر spec در همان تغییر.
- R-D3. Agent-driven: `AGENTS.md` با قواعد و best practice در هر سه.
- R-D4. جفت‌های دو زبانه `.md` و `.fa.md` همگام.
- R-D5. قواعد best practice طبق توضیح مالک، نه کپی کورکورانهٔ sample.

## هـ. فرایند
- R-E1. اول مستندات و spec و قواعد و roadmap؛ هیچ تصمیم و نکته‌ای از دست نرود.
- R-E2. roadmap داخل پروژه، فازبندی‌شده و تیک‌خورده؛ با تمام شدن credit نقطهٔ ادامه مشخص باشد.
- R-E3. فاز به فاز، با commit و push روی `claude/exciting-fermi-b41fue`.
- R-E4. ایرادهای sample اصلاح شوند، کپی نشوند.
- R-E5. درخت نهایی به مالک نمایش داده شود (`docs/specs/architecture.md`).

## F. دکتر (ریپوهای موجود)

- R-F1. ‏`dotnet-arch doctor [path]` یک ریپوی موجود را بدون تغییر با استاندارد می‌سنجد: لایه‌ها و جهت وابستگی، پروژه‌های تست، بهداشت build، پیکربندی و secretها، Docker/CI، مستندات دوزبانه، قواعد کد و مرز kitها.
- R-F2. قواعد سازمانی از یک پروفایل اعلانی می‌آید (`.net-arch/profile.yml` یا فایل مشترکی که به آن ارجاع می‌دهد)؛ ابزار هیچ بررسی مخصوص سازمان ندارد. پروفایل می‌تواند layoutهایی غیر از v2 را بپذیرد.
- R-F3. خروجی برای انسان (متن) و agent ‏(`--json`، ابزار MCP به نام `doctor`)؛ هر finding شناسه، شدت، مکان و راه‌حل دارد. کد خروج `3` وقتی مسدودکننده است (خطا، یا هشدار با `--strict`).

## G. جذب و استقلال

- R-G1. ‏`adopt` پروژهٔ موجود را فقط با نوشتن `.net-arch/` (state مشتق‌شده از سورس، قواعد، ارجاع پروفایل، generated.lock) تحت کنترل ابزار می‌آورد؛ هیچ فایل سورسی را تغییر نمی‌دهد و تکرارپذیر است.
- R-G2. پروژه هرگز به ابزار وابسته نیست: حذف `.net-arch/` نه build، نه تست، نه Docker، نه CI، نه مستندات و نه MCP را می‌شکند.
- R-G3. ‏`fix` استاندارد را فقط جایی برمی‌گرداند که تغییر مکانیکی است و رفتار را عوض نمی‌کند (فایل‌های بهداشتی، خط‌های ignore)؛ اول طرح می‌دهد و فقط با `--apply` می‌نویسد.
- R-G4. یک رجیستری عملیات هر عملیات را یک‌بار تعریف می‌کند؛ CLI و MCP از آن مشتق می‌شوند. عملیات تغییردهنده طرح ماشین‌خوان برمی‌گرداند.
- R-G5. تنظیمات: پیش‌فرض ابزار < سراسری `~/.net-arch/` < پروژه `.net-arch/`؛ rules.yml تغییر شدت، آستانه‌ها و استثنا با دلیل اجباری را نگه می‌دارد.
- R-G6. ‏`doctor` مستندات (فهرست، specها، snapshot ‏OpenAPI) و تست‌ها (وجود تست، پوشش در برابر آستانه) را هم اعتبارسنجی می‌کند.

### هم‌ترازی با ABP (R-H)
- R-H1. layout v3 ‏(`src/`، `test/`، `etc/`) در `adopt`، `doctor` و `fix` پشتیبانی می‌شود؛ v2 معتبر می‌ماند؛ ‏`fix --rules=DA-A01` از v2 به v3 می‌برد و همهٔ مسیرهای اشاره‌کننده به پوشه‌های جابه‌جاشده را بازنویسی می‌کند.
- R-H2. پروژه‌های لایه ‏`Domain.Shared`، `Application.Contracts`، `HttpApi`، `HttpApi.Client` اختیاری‌اند و فقط جایی وجود دارند که چیزی برایشان هست؛ ‏`fix --rules=DA-A02` لایه را همراه فایل‌هایی می‌سازد که به آن می‌روند (namespace و مسیر داخل پروژه ثابت، ارجاع‌ها و پکیج‌ها و خطوط restore در Dockerfile وصل‌شده)، آنچه نمی‌تواند برود را با فایل مانع فهرست می‌کند، controllerها را با هم می‌برد یا اصلاً نمی‌برد و لایهٔ خالی را فقط با `--empty` می‌سازد. ‏`doctor` با DA-A07 پروژهٔ لایه بدون سورس را گزارش می‌کند.
- R-H3. ‏`doctor` مجموعه‌قاعدهٔ داخلی و اختیاری `abp` ‏(`DA-A01..DA-A11`) دارد که با `standards: [abp]` فعال می‌شود؛ پروفایل می‌تواند الزامش کند و استثناهای پذیرفته‌شده را با دلیل فهرست کند.
- R-H4. ‏`new solution --layout=v3` درخت هم‌شکل ABP را تولید می‌کند (اختیاراً با پروژه‌های لایه) و مجموعه‌قاعدهٔ `abp` را می‌گذراند.
- R-H6. ‏`fix --rules=DA-A08` پروژهٔ کلاینت تایپ‌دار را از روی controllerها تولید می‌کند (فقط نوع‌های contract؛ اکشن‌های پشتیبانی‌نشده دستی فهرست می‌شوند)؛ ‏`doctor` با DA-A08 سرویسِ دارای controller و بدون کلاینت را گزارش می‌کند.
- R-H8. ‏`doctor` با DA-A10 فایل‌های رهاشده در ریشهٔ پروژه و پوشه‌های شلوغ از چند فیچر را گزارش می‌کند؛ ‏`fix --rules=DA-A10` آن‌ها را به پوشه‌های نوع و فیچر در همان پروژه می‌برد (namespaceها دست‌نخورده)؛ کلاینت تایپ‌دار در همین درخت تولید می‌شود.
- R-H9. ‏`doctor` با DA-A11 ارجاع‌های پروژه (قابل دسترس، رعایت‌کنندهٔ جهت، بدون ارجاع ممنوع بی‌استفاده)، عضویت در solution و خط‌های restore در Dockerfile را بررسی می‌کند؛ ‏`fix --rules=DA-A11` درستشان می‌کند و ارجاع ممنوعِ استفاده‌شده را دستی گزارش می‌کند.
- R-H7. ‏`fix --rules=DA-A09` فایل‌های Compose ریشه را به `etc/docker/` می‌برد، مسیرهای نسبی را بازپایه می‌کند و اشاره‌ها را به‌روز می‌کند؛ ‏`doctor` با DA-A09 فایل‌های Compose ریشه را گزارش می‌کند.
- R-H5. ابزار قاعده‌ای دربارهٔ idهای `Guid`، application service یا قابلیت‌های زمان اجرای ABP ندارد.
- R-I1. همهٔ دستورها (مولدها، عملیات، بازرسی) عملیات یک رجیستری‌اند؛ سطح CLI (`new crud`) و MCP (`new_crud`) از آن مشتق می‌شوند و فهرست و مقدارهای یکسان دارند. دستوری که فایل تغییر می‌دهد طرح برمی‌گرداند و فقط با apply می‌نویسد.
- R-I2. ‏`add_layer`، `add_tests`، `spec_list|add|check` و `graph` عملیات رجیستری‌اند؛ ‏`add_layer` فقط وقتی فایلی به لایه منتقل می‌شود آن را می‌سازد و لایهٔ خالی فقط CLI است (`--empty`).
- R-I3. ‏`new solution` پیش‌فرض layout v3 دارد؛ در پروژهٔ ABP بعد از هر مولد fixerهای ABP و تازه‌سازی `.net-arch/` اجرا می‌شود و solution تولیدشده بدون هشدار build می‌شود، تست‌هایش پاس است و یافتهٔ ABP ندارد.
- R-I4. قالب‌های تولید fixer-friendly‌اند: DTO داده‌ی خالص است، نگاشت entity فایل جدا دارد، پورت repository async است و `IQueryable` برنمی‌گرداند.
- R-I5. ‏کلاینت تایپ‌دار همراه تست‌هایش تولید می‌شود (verb و route هر action و یک تست برابری که هر route کنترلر متد کلاینت دارد) و از routeهای controller پیروی می‌کند: actionی که بعداً اضافه شود، از جمله در فایل partial، به کلاینت و تست‌هایش می‌رسد؛ ‏`doctor` با DA-A08 کلاینت ناموجود یا کهنه را گزارش می‌کند.
- R-I6. ‏پروفایل می‌تواند پوشه‌های ریشه‌ای را که باید جابه‌جا شوند فهرست کند (`folder_moves`)؛ ‏`doctor` با DA-A12 جابه‌جایی معلق را گزارش می‌کند و `fix --rules=DA-A12` آن را می‌برد و مسیرهای اشاره‌کننده را بازنویسی می‌کند، بی‌آنکه مسیرهای زمان اجرا، مسیر کلیدها، متادیتای `Link`، کد یا اسناد تاریخچه را دست بزند.

### توانمندسازی سرویس‌های Corevia (R-J)
- R-J1. ‏`new solution --in-place` solution را مستقیم در پوشهٔ خروجی می‌سازد (پوشهٔ ریپویی که از قبل هست، مثل `corevia-<slug>`)؛ فایل موجود هرگز بازنویسی نمی‌شود.
- R-J2. ‏`new action` می‌تواند use case تایپ‌دار بسازد: ‏`--input=Name:Type,...` و `--output=Name:Type,...|Type|none` به درخواست propertyهای خودش و نتیجهٔ خودش را می‌دهند (scalar، آرایه، list، dictionary، نام DTO)؛ handler یک stub است، action کنترلر، validator، ابزار MCP، دستور CLI و کلاینت تایپ‌دار همراهش می‌آیند و use case در `dotnet-arch.yml` ثبت می‌شود تا میزبان‌های بعدی آن را دوباره بسازند. ‏`--anonymous` نقطهٔ عمومی را علامت می‌زند.
- R-J3. ‏`add_cli` (و `new solution --cli`) میزبان CLI ‏`src/<App>.Cli` را اضافه می‌کند، هم‌سطح میزبان MCP: هر دو مستقیم لایهٔ Application را با همان درخواست‌های MediatR که API استفاده می‌کند صدا می‌زنند؛ هر entity، action و use case تایپ‌دار یک دستور می‌گیرد؛ وقتی هر دو میزبان هستند یک تست برابری مجموعهٔ نام ابزارهای MCP و نام دستورهای CLI را یکی نگه می‌دارد.
- R-J4. ‏پروفایل می‌تواند kitهای منتشرشده را فهرست کند (`external_kits`: بسته‌ها با نسخهٔ مرکزی و پروژه‌هایی که ارجاعشان می‌دهند، برای هر host خط‌های `usings` / `bootstrap` / `builder` / `app`، ‏`settings`، ‏`secrets`، ‏`test_settings`، ‏`replaces`)؛ ‏`new solution --profile=<file>` آن‌ها را در hostهای Api و MCP و CLI سر markerهای `Program.cs` wire می‌کند، تنظیمات، کلیدهای `.env.example`، قرارداد پیکربندی و مقدارهای factory تست را می‌نویسد، بخش‌های تولیدشده‌ای را که kit جایگزین می‌کند (`swagger`، `exception-handler`) برمی‌دارد و پروفایل را در `.net-arch/profile.yml` ارجاع می‌دهد. ابزار نام هیچ kitی را نمی‌داند؛ اجرای دوباره wiring چیزی را تغییر نمی‌دهد.
- R-J5. ‏`new entity --entity= --properties=Name:Type[(max)][?][!|#],...` یک entity دامنه با propertyهای تایپ‌دار (طول string، enum، اختیاری، ایندکس یکتا یا ساده) و persistenceش را بدون use caseهای CRUD تولید می‌کند: aggregate با factory ‏`Create`، نگاشت EF، DTO و mapping آن، تست دامنه و تست persistence (رفت‌وبرگشت و ایندکس‌های یکتا). ‏`new enum --members=A,B` عضوها را بعد از `None = 0` تولیدشده اضافه می‌کند. پورت repository متدهای `FirstOrDefaultAsync` و `ListAsync` را دارد.
- R-J6. ‏`new feature --feature=Setting` گروه use case بدون entity (اسکلت کنترلر و ابزار MCP) تولید می‌کند؛ use caseهای تایپ‌دار با `new action --entity=<Feature> --input --output` به آن وصل می‌شوند (output باید صریح باشد).
- R-J7. ‏`apply_profile --profile=<file>` ‏`external_kits` یک پروفایل مشترک را با همان قواعد `new solution --profile` در solution موجود wire یا ارتقا می‌دهد (نسخهٔ kitها، خط‌های host در markerها، تنظیمات، کلیدهای secret، مقدارهای تست)؛ idempotent است، اول طرح می‌دهد و فقط با apply می‌نویسد. مقدار نمونهٔ propertyهای رشته‌ای هرگز از طول اعلام‌شده بیشتر نمی‌شود، پس تست‌های تولیدشده برای propertyی با `string(8)` هم entity می‌سازند.
- R-J8. ورودی typed می‌تواند طولی را که رشته‌اش با آن اعتبارسنجی می‌شود مشخص کند (‏`--input=Document:string(8000)`؛ پیش‌فرض ۵۱۲) و مثل بقیهٔ use case در `dotnet-arch.yml` ثبت می‌شود. وقتی controllerهای یک feature در لایهٔ HttpApi هستند، `new entity` و `new feature` و `new action` دیگر controller جزئی را دوباره در پروژهٔ Api اعلام نمی‌کنند (کلاس دوم روی همان route).
- R-J9. ‏`apply_blueprint --blueprint=<file>` آنچه یک blueprint (‏YAML) توصیف می‌کند را در یک طرح تولید می‌کند: ‏`profiles` (مثل `apply_profile`)، ‏`enums`، ‏`entities`، ‏`features`، ‏`use_cases` ‏(entity، action، method، input، output، anonymous، description؛ قواعد use case typed در R-J2 و R-J8)، ‏`services` (سرویس‌های داخلی) و `layers` (مثل `add_layer`) به همین ترتیب و با همان مولدهای دستورهای تکی. آنچه از قبل هست (enum، entity، feature، use case ثبت‌شده، فایل سرویس) رد می‌شود، پس بعد از رشد blueprint می‌توان دوباره اجرایش کرد؛ نام جاافتاده پیش از نوشتن هر چیزی رد می‌شود. blueprint مشخصات سرویس است: در ریپوی سرویس (`docs/specs/blueprint.yaml`) می‌ماند و CLI و ابزار MCP از یک ورودی registry می‌آیند. طول ورودی رشته‌ای typed در `dotnet-arch.yml` ثبت می‌شود (‏`Name:string(120)`) و یک use case می‌تواند DTO entity دیگری از solution را برگرداند.
- R-J10. پورت repository تولیدشده می‌تواند تازه‌ترین را اول صفحه‌بندی کند (‏`GetPagedAsync(..., newestFirst: true)`) و بشمارد (‏`CountAsync(predicate)`)، هم در adapter ‏EF و هم در همهٔ fakeهای تولیدشده، تا use caseهای فهرست و آمار به query جداگانه نیاز نداشته باشند.
- R-J11. ‏blueprint نسخه دارد. ‏`schema` (قالب فایل؛ ابزار قالب جدیدتر از دانستهٔ خود را رد می‌کند) و `version` (‏semver، نسخهٔ مدل) را دارد. ‏`apply_blueprint` آنچه اعمال کرده را در `.net-arch/service-blueprint.lock` ثبت می‌کند (فایل، schema، نسخه، هش محتوا)، نسخهٔ پایین‌تر از اعمال‌شده را رد می‌کند و فایل تغییر‌کرده با همان نسخه را هم رد می‌کند، تا هر تغییر مدل نسخهٔ تازه داشته باشد. ‏doctor بدون وابستگی به هیچ ریپوی استاندارد blueprint را بررسی می‌کند: ‏DA-BP01 (نبودن blueprint)، DA-BP02 (خوانده نمی‌شود یا schema/version ندارد)، DA-BP03 (توصیف‌شده ولی تولیدنشده)، DA-BP04 (entity یا use case تولیدشده که blueprint توصیف نکرده)، DA-BP05 (با نسخهٔ فعلی اعمال نشده). ‏`fix --rules=DA-BP03,DA-BP05` با همان مولدها blueprint را اعمال می‌کند و خود blueprint را هرگز ویرایش نمی‌کند.
- R-J12. ‏doctor و fix: ‏DA-A08 ‏GETی را که رکورد query را با `[FromQuery]` می‌گیرد بیان می‌کند (یک مقدار query-string برای هر property، ابتدا الزامی‌ها)؛ ‏DA-C04 فقط وقتی یافته است که فایل `.env` در `.gitignore` نباشد؛ ‏DA-M09 با یک تست تولیدشده (‏`OpenApiSnapshotTests`) اصلاح می‌شود که سند OpenAPI در حال اجرای API را با `docs/api/openapi.json` مقایسه می‌کند و با `UPDATE_OPENAPI_SNAPSHOT=1` آن را می‌نویسد؛ اعمال blueprint طول‌های رشتهٔ ثبت‌شده برای use caseهای موجود در `dotnet-arch.yml` را با blueprint هماهنگ می‌کند.
