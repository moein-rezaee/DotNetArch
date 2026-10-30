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
- R-J13. ‏`fix --rules=DA-BP01` برای سرویسی که پیش از داشتن blueprint ساخته شده، blueprint می‌نویسد: ‏enumها، entityها (propertyهای typed با طول، nullable بودن، شاخص یکتا و ساده و propertyهای بیرون از DTO)، featureها، use caseها (طبق ثبت در `dotnet-arch.yml`) و سرویس‌ها از کد تولیدشده خوانده و اعمال می‌شوند (lock نسخهٔ 1.0.0 را ثبت می‌کند). نقطهٔ شروعی برای بازبینی است؛ entityهای `new crud` در blueprint توصیف نمی‌شوند.
- R-J14. ‏`fix --rules=DA-D03` مرحلهٔ نهایی Dockerfile که روی image اجرای .NET ‏(`dotnet/aspnet`، `dotnet/runtime`) ساخته شده را با کاربر غیر root همان image اجرا می‌کند: ‏`USER $APP_UID` پیش از اولین ENTRYPOINT یا CMD مرحلهٔ آخر می‌آید. هر image پایهٔ دیگر یافتهٔ دستی می‌ماند. ابزار MCP به نام `fix` و فرمان CLI یک عملیات‌اند.
- R-J15. سرویسی که با ابزار تولید نشده (فقط adopt شده) به‌صورت مکانیکی به همان سطح می‌رسد. ‏`dotnet-arch.yml` از فایل solution، پروژهٔ Api، پکیج‌ها، Dockerfile ‏(`EXPOSE`)، ‏`NuGet.config` و `.net-arch/project.yml` خوانده می‌شود (doctor ‏DA-G01 و `fix --rules=DA-G01`) و بعد همهٔ مولدها و `apply_blueprint` روی آن کار می‌کنند. میزبان MCP فقط وقتی ثبت می‌شود که از خود ابزار باشد (`Tools/ToolRunner.cs`): ابزار هرگز در میزبان دست‌نویس تولید نمی‌شود. plan فایل‌ها را با محتوا مقایسه می‌کند، پس نوشتن دوبارهٔ همان بایت‌ها تغییر نیست.
- R-J16. ‏`add_persistence` به سرویسی که context ‏EF ندارد اسکلت استاندارد persistence را با همان قالب‌های `new_solution` می‌دهد (نوع‌های پایهٔ دامنه، پورت‌های repository و unit of work، آداپتور EF، context، factory زمان طراحی، pipeline ‏MediatR و پشتیبانی تست)، پکیج‌ها را ادغام می‌کند (pin مرکزی قدیمی‌تر و `VersionOverride` قدیمی‌تر `Microsoft.Extensions.*` بالا می‌رود، هرگز پایین نمی‌آید)، تنظیمات `Database` را می‌نویسد و `Program.cs` را وصل می‌کند؛ فایل موجود هرگز بازنویسی نمی‌شود و سرویسی که از قبل context ‏EF دارد رد می‌شود.
- R-J17. ‏`add_tenancy --profile=<profile دارای kit ‏Tenancy>` سرویس را چندمستاجری می‌کند (مستاجر = realm در Keycloak): kit وصل می‌شود (حتی در `Program.cs` بدون نشانگر)، هر entity که تنها context ‏EF ذخیره می‌کند `ITenantOwned` و `TenantId` می‌گیرد، context هر پرس‌وجو را با مستاجر صدازننده فیلتر می‌کند (اپراتور و context بدون صدازننده همهٔ مستاجرها را می‌بینند)، مستاجر را روی ردیف تازه می‌نویسد و ردیف بدون مستاجر را رد می‌کند. قاعدهٔ doctor با نام DA-TN01 انتخابی است (`adopt --standards=tenancy`) و `fix --rules=DA-TN01 --tenancy_profile=<profile>` این عملیات را اجرا می‌کند. آنچه با متن قابل patch نیست دستی فهرست می‌شود.
- R-J18. سه fix مکانیکی دیگر: ‏DA-K04 (میزبانی که هر مبدأ را می‌پذیرد مبدأهای `Cors:AllowedOrigins` را می‌پذیرد)، ‏DA-M09 برای سرویسی که تست‌هایش میزبان درون‌فرایندی ندارد (یک میزبان کوچک روی controllerهایش سند OpenAPI را می‌سازد) و fix ‏Dockerfile از R-J14.
- R-J19. دو قاعدهٔ دیگر در doctor همراه با fixer: ‏DA-M10 (هر دو سند یک جفت انگلیسی/فارسی نزدیک بالا به هم لینک دارند؛ ‏`fix` خط را اضافه می‌کند) و DA-B10 (پروژهٔ Api یک `<Version>` دارد که هر انتشار آن را بالا می‌برد؛ ‏`fix` نسخهٔ `1.0.0` را اضافه می‌کند). قالب‌های README و docs تولیدشده لینک زبان، بخش‌های استاندارد README ‏(Purpose، Scope، Prerequisites، Configuration، Run / Usage، Validation / Verification، Troubleshooting، Change Log، Ownership) و نسخه را دارند، پس سرویس تولیدشده بدون کار دستی از دروازهٔ مستندات و نسخه می‌گذرد.
- R-J20. ابزار بسته‌های مشترکی را می‌سازد که سرویس‌ها پورت‌هایشان را به آن‌ها وصل می‌کنند. ‏`scaffold_kit` یک ناحیهٔ Kit در ریپوی Kit می‌سازد (بسته‌های Abstractions و Core، پروژهٔ تست، README و AGENTS و specs دوزبانه و ورودی `Corevia.Kit.packages.proj`)؛ ‏`scaffold_provider` یک بستهٔ ارائه‌دهنده به ناحیه اضافه می‌کند (پروژه، افزونهٔ DI به نام `AddCorevia<Kit><Provider>`، ارجاع از پروژهٔ تست، ورودی فهرست بسته‌ها و فهرست‌های مستندات). ناحیه یا ارائه‌دهندهٔ موجود رد می‌شود؛ نام‌ها PascalCase هستند. هر دو از یک registry در CLI و MCP هستند. کنترلری که `[ApiExplorerSettings(IgnoreApi = true)]` دارد (webhook برای یک پلتفرم) جزو کلاینت تایپ‌شده نیست (DA-A08).
- R-J21. ‏`add_tenancy --wire_only` فقط kit چندمستاجری را وصل می‌کند (بسته‌ها و خط‌های host) برای سرویسی که داده‌اش در یک سیستم مرجع بیرونی (سیستم حسابداری) است: ستون مستاجر برای افزودن نیست، مستاجر تماس‌گیرنده اتصال را انتخاب می‌کند.
- R-J22. ‏migration که `dotnet ef migrations add` می‌نویسد از `.editorconfig` راه‌حل پیروی می‌کند: EF فایل را UTF-8 همراه byte order mark می‌نویسد، پس وقتی editorconfig `charset = utf-8` ساده بخواهد ابزار این علامت را از فایل‌های migrationِ تازه‌تولیدشده برمی‌دارد (و برای `utf-8-bom` نگه می‌دارد). سرویس تازه بدون فرمت دستی از `dotnet format whitespace --verify-no-changes` (کار lint) رد می‌شود.
- R-K1. یک ماژول به‌صورت مکانیکی قابل ترکیب می‌شود (فاز ۱۵، D-40). ‏`move_code` فایل‌ها را بین پروژه‌ها (یا به یک پوشه در همان پروژه) جابه‌جا می‌کند و namespace، همهٔ `using`ها و نام‌های کامل و نیمه‌کامل را می‌نویسد، ارجاع پروژه و بسته‌ای را که کد جابه‌جاشده لازم دارد اضافه می‌کند (namespace با assemblyهای پوشهٔ بستهٔ restore‌شده سنجیده می‌شود، نه با نام بسته) و با `--make_public` نوع‌های جابه‌جاشده را public می‌کند. ‏using یک namespaceای که دیگر نیست حذف می‌شود و کدی که در namespace قدیم بود usingی را که حالا لازم دارد می‌گیرد.
- R-K2. ‏`add_composition --adapters=A,B` پروژهٔ `<App>.Composition` را با `Add<Module>Composition(configuration, registerBackgroundWorkers)` می‌سازد: Infrastructure ماژول و آداپتری را که با یک کلید تنظیمات انتخاب می‌شود ثبت می‌کند (پیش‌فرض `ServiceProvider:DefaultProvider`؛ `--selector_env` و `--aliases` و `--regenerate` نام‌های تاریخی را نگه می‌دارند)، فراخوانی‌های بیرون از Infrastructure را عوض می‌کند و ارجاع پروژه‌ها را اضافه می‌کند. آداپتر اول پیش‌فرض است.
- R-K3. ‏`describe_module` فایل `module.yaml` را می‌نویسد (پورت‌ها، آداپترها با بسته، نسخه، متد ثبت، پورت‌هایی که پیاده می‌کنند و کلیدهای تنظیماتی که می‌خوانند، لایه‌ها، پروژهٔ composition، نسخهٔ ماژول) و `describe_settings` فایل `settings.schema.json` را کنار هر پروژه‌ای که کلاس options دارد می‌نویسد (JSON Schema 2020-12، بسته، سکرت‌ها `writeOnly`، کلاس‌های تودرتو داخل هم). هر دو استخراجی هستند: فقط وقتی کد عوض شود عوض می‌شوند.
- R-K4. مجموعه‌قاعدهٔ آداپترها (`standards: [adapters]`، DA-AD01 تا DA-AD06): آداپتر (`<App>.Infrastructure.<System>`) فقط به هستهٔ ماژول وابسته است؛ Infrastructure مشترک به هیچ آداپتری ارجاع نمی‌دهد؛ آداپترها به هم ارجاع نمی‌دهند؛ آداپتر متد ثبت دارد؛ آداپتر یک بسته است و نامش از پروفایل می‌آید (`adapter_package`، هیچ سازمانی در ابزار نیست)؛ بسته اسکیمای `settings.schema.json` خودش را دارد. کلید پروفایل `non_adapters` پروژه‌های Infrastructure را که ماندگاری هستند نام می‌برد. ‏`fix` ارجاع بی‌استفاده را برمی‌دارد، بسته‌هایی را که پروژهٔ برداشته‌شده می‌آورد مستقیم نگه می‌دارد و PackageId و آیتم اسکیما را می‌نویسد.
- R-K5. ‏`scaffold_adapter` یک بستهٔ آداپتر به ناحیهٔ Kit اضافه می‌کند (`Corevia.Kit.<Kit>.Adapters.<Adapter>`)، مثل `scaffold_provider` با واژه‌های مدل کامپوزیشن (که یک انتشار می‌ماند)؛ و قاعدهٔ آداپتر DA-AD07 کلید تنظیماتی را گزارش می‌کند که آداپتر می‌خواند ولی `settings.schema.json` آن تعریفش نکرده (کلید تخت `KEY_LIKE_THIS` را اسکیمای سلسله‌مراتبی هیچ‌وقت تعریف نمی‌کند: همان بدهی TD-06 که حالا دیده می‌شود).
- R-K6. ‏DA-AD08: پروژه‌های هستهٔ ماژول که آداپتر به آن‌ها وابسته است (Domain، Domain.Shared، Contracts، Application.Contracts و آنچه این‌ها ارجاع می‌دهند) بسته‌ای با نسخهٔ ماژول هستند (`<Version>` پروژهٔ Api) و نامشان از الگوی پروفایل `module_package` می‌آید (توکن‌های `{Module}` و `{Layer}`)؛ وگرنه بستهٔ آداپتر به بسته‌ای وابسته می‌شود که هیچ feedی ندارد. ‏`fix --rules=DA-AD08` مقدار PackageId و IsPackable و نسخه را می‌نویسد.
