# قراردادها (CLI، پیکربندی، MCP)

## CLI
دستورها (همه عملیات رجیستری‌اند، D-32؛ دستور دوکلمه‌ای همان عملیات با `_` است: ‏`new crud` = `new_crud`): ‏`new solution`، `new crud`، `new action`، `new event`، `new enum`، `new constant`، `new service`، `new kit`، `add kit`، `add mcp`، `add cli`، `add layer`، `add tests`،
‏`new entity`، `new feature`، `ci add`، `docker add`، `git setup`، `spec list`، `spec add`، `spec check`، `graph`، `list entities`، `describe config`، `describe module`، `describe settings`، `move code`، `add composition`، `add migration`، `exec`، `remove migration`، `doctor`، `adopt`، `fix`، `mcp serve`.
هر دستوری که فایل تغییر می‌دهد اول طرح می‌دهد و فقط با `--apply` می‌نویسد؛ در ترمینال طرح نشان داده می‌شود و از کاربر پرسیده می‌شود. ‏`--json` خروجی `{ ok, operation, applied, error?, plan[], data }` را چاپ می‌کند و `--out=file` آن را ذخیره می‌کند. ‏`--path` (معادل `--output`) پوشهٔ solution است؛ مقدارها را می‌توان `--no-migration` یا `--no_migration` نوشت. مقدار الزامیِ نداده‌شده پرسیده می‌شود.

گزینه‌های `new solution <Name>`: `--output`، `--database=SQLite|SqlServer|Postgres`، `--style=controller|fast`، `--layout=v2|legacy` (پیش‌فرض v2)، `--mcp`، `--cli`، `--in-place`،
`--ci=auto|none|github|gitlab|azure|bitbucket|gitea`، `--git-remote`، `--git-provider`، `--git-host`، `--docker-registry`، `--nuget-source`، `--nuget-source-name`،
`--no-docker`، `--no-git`، `--no-tests`. فریم‌ورک هدف از بالاترین SDK نصب‌شده (۸ یا ۹) گرفته می‌شود.

`new action --entity= --method= [--action=] [--input=Name:Type,...] [--output=Name:Type,...|Type|none] [--anonymous] [--description=]`: بدون `--input`/`--output` همان action کلاسیکِ مبتنی بر id؛ با آن‌ها use case تایپ‌دار (نیازمند `--style=controller`). نوع‌ها: string، int، long، bool، decimal، double، float، Guid، DateTime، DateTimeOffset، TimeSpan، ‏`T[]`، ‏`List<T>`، ‏`Dictionary<string,T>`، ‏`?` پایانی یا نام DTO. use case در `dotnet-arch.yml` به شکل `usecase.<Entity>.<Action>: METHOD|anonymous|protected|input|output` ذخیره می‌شود.

`new solution --profile=<file>`: ‏`external_kits` پروفایل را wire می‌کند (R-J4). شکل پروفایل: `external_kits: [{ area, packages: [{ id, version, projects: [api|mcp|cli|application|infrastructure] }], hosts: { api|mcp|cli: { usings, bootstrap, builder, app } }, settings: { "Section:Key": value }, secrets: [KEY], test_settings: { KEY: value }, replaces: [swagger|exception-handler] }]`؛ ‏`{{App}}` در یک خط نام solution است. markerهای `Program.cs`: ‏`kit-usings`، `kit-bootstrap`، `kit-builder`، `kit-app`؛ factoryهای تست: `kit-test-settings`.

`new entity --entity= --properties=Name:Type[(max)][?][!|#],...`: نوع propertyها string (طول با `(n)`، پیش‌فرض ۲۵۶)، int، long، bool، decimal، double، float، Guid، DateTime، DateTimeOffset، TimeSpan یا نام یک enum (به‌صورت string ذخیره می‌شود)؛ ‏`?` اختیاری، ‏`!` ایندکس یکتا، ‏`#` ایندکس، ‏`~` بیرون از DTO (هش، secret)؛ ‏`Id` و `CreatedAtUtc` و `UpdatedAtUtc` از کلاس پایه می‌آیند. ‏`dotnet-arch.yml` مقدار `entity.<Name>: model` را ثبت می‌کند. ‏`new feature --feature=<Name> [--description=]` مقدار `entity.<Name>: feature` را ثبت می‌کند. ‏`new enum --members=A,B`.

`add cli` / `new solution --cli`: ‏`src/<App>.Cli` (کنسول؛ نام assembly همان نام kebab-case solution)، دستورهای `<entity> list|get|create|update|delete`، ‏`<entity> <action>` و `<entity> <use-case> --option=value`؛ کد خروج ۰ انجام شد، ۱ رد شد (validation، پیدا نشد، قاعدهٔ دامنه)، ۲ خطای استفاده؛ نتیجه JSON است. ‏`dotnet-arch.yml` مقدار `cli: true` را ثبت می‌کند.

`new service`: با منطق کسب‌وکار (`--logic=true --name=`) یک سرویس Application تولید و در `AddApplication()` ثبت می‌کند؛ بدون منطق (`--logic=false --area= [--providers=]`) یک kit می‌سازد و wire می‌کند.
بدون `--logic` تعاملی است.

`new kit --area=<Area> [--providers=A,B] [--kit-prefix=P] [--with-tests] [--no-wire]` و `add kit <Area>`. حوزه‌های آماده: MediaStorage (Minio، RustFs)، Cache (InMemory، Redis)،
MessageBroker (RabbitMq)؛ هر نام دیگر اسکلت عمومی می‌گیرد. `new crud` و `new action` گزینهٔ `--no-migration` دارند.

همهٔ آرگومان‌ها به‌صورت لیست به فرایند فرزند می‌رسند (بدون shell). نام‌ها پیش از استفاده اعتبارسنجی می‌شوند. پرسش‌های تعاملی با ورودی pipe شده با شمارهٔ گزینه یا متن پاسخ داده می‌شوند؛ خط خالی یعنی پیش‌فرض.

## کلیدهای dotnet-arch.yml
`solution path startup style port framework database layout ci.provider git.provider git.host docker.image docker.container docker.registry nuget.source nuget.sourceName
kit.prefix kit.<Area> mcp entity.<Name>`. نبودن `layout` یعنی چیدمان قدیمی.

## قرارداد پیکربندی (کد تولیدشده)
- غیرحساس: `appsettings.json`. secret و مقادیر runtime: environment یا `.env` (UPPER_CASE، `__` برای تودرتویی).
- `AddAppConfiguration()` (در Infrastructure، مشترک بین Api و Mcp) منابع را به ترتیب بارگذاری می‌کند (بعدی غالب است) پیش از هر options binding.
- `.env.example` فهرست کامل secretها با مقدار placeholder است؛ `appsettings.example.json` آینهٔ `appsettings.json` است؛ `ConfigurationContract.SecretKeys` secretها را فهرست می‌کند؛ تست‌های `Category=Configuration` هر سه را اجباری می‌کنند.
- کلیدهای kit: `<Area>:Provider` و `<Area>:<Provider>:<Option>`؛ secretها `<PROVIDER>_<NAME>` (مثل `REDIS_PASSWORD`).

## MCP: سرور ابزار
`dotnet-arch mcp serve` (stdio). هر عملیات رجیستری یک ابزار با همان نام، توضیح و مقدارهای دستور CLI است (`new_solution`، `new_crud`، `new_action`، `new_event`، `new_enum`، `new_constant`، `new_service`، `new_kit`، `add_kit`، `add_mcp`، `add_layer`، `add_tests`، `ci_add`، `docker_add`، `git_setup`، `spec_list`، `spec_add`، `spec_check`، `graph`، `list_entities`، `describe_config`، `describe_module`، `describe_settings`، `move_code`، `add_composition`، `split_registration`، `add_adapter`، `add_migration`، `exec`، `remove_migration`، `doctor`، `adopt`، `fix`). غیرتعاملی و بدون shell. ابزارهای فقط‌خواندنی علامت read-only دارند؛ هر ابزار تغییردهنده پرچم `apply` دارد (پیش‌فرض false: فقط طرح) و `{ ok, operation, applied, error?, plan[], data }` برمی‌گرداند. پارامترهای فقط‌CLI (مثلاً `empty` در `add_layer`) ارائه نمی‌شوند. هیچ ابزاری destructive نیست؛ ‏`add_migration`، `remove_migration` و `exec` فقط با `apply: true` عمل می‌کنند.

## MCP: host تولیدشده
`src/<App>.Mcp` با streamable HTTP روی `/mcp` (stateless) و `/health` باز. احراز هویت: bearer token با مقایسهٔ constant-time با `MCP_AUTH_TOKEN` (حداقل ۲۴ نویسه).
ابزارها به‌ازای entity: `<entity>_list|get|create|update|delete` و به‌ازای action: `<entity>_<action>`؛ هر ابزار همان درخواست MediatR controller را می‌فرستد.

## کدهای خروج و خروجی
`0` موفق، `1` خطای استفاده/اعتبارسنجی، `2` نبودن .NET SDK. لاگ انسانی در CLI روی stdout و در `mcp serve` روی stderr می‌رود.

## دکتر
‏`dotnet-arch doctor [path] [--profile=auto|generic|corevia] [--json] [--strict] [--out=file]` و ابزار MCP به نام `doctor(repositoryPath, profile, json)`. فقط‌خواندنی: هیچ فایلی نوشته نمی‌شود (جز `--out`)، فرایندی اجرا نمی‌شود، .NET SDK لازم نیست.
Finding: ‏`{ id: "DA-<area><nn>", severity: error|warning|info, category, message, path?, hint?, details?[] }`. شناسه‌ها: S ساختار، B build، C پیکربندی، D کانتینر/CI، M مستندات، K کد، V پروفایل corevia.
کدهای خروج: `0` سالم، `1` خطای استفاده، `3` finding مسدودکننده.

## رجیستری عملیات، adopt، fix، .net-arch
‏`doctor`، `adopt` و `fix` یک‌بار در رجیستری عملیات تعریف شده‌اند؛ CLI ‏(`dotnet-arch <op> [path] [--json] [--apply] [--out=file]`) و ابزارهای MCP ‏(`doctor`، `adopt`، `fix`) از آن مشتق می‌شوند. خروجی `--json` / نتیجهٔ MCP به شکل `{ ok, operation, applied, error?, plan[], data }` است؛ هر آیتم طرح `{ path, action: create|modify, reason, ruleId? }` است. عملیات تغییردهنده فقط با `--apply` (در MCP ‏`apply: true`) می‌نویسند.
‏`adopt [path] [--profile=file]` فقط `.net-arch/project.yml` ‏(schema، blueprint، layout، mcp، layers، kits، entities، modules)، `rules.yml` ‏(یک‌بار)، `generated.lock` ‏(یک‌بار) و با `--profile` فایل `profile.yml` ‏(یک‌بار، `source` نسبت به ریشهٔ ریپو) را می‌نویسد. ‏`fix [path] [--rules=ids]` به‌طور پیش‌فرض DA-B01 ‏(global.json)، DA-B05 ‏(نشانگر .editorconfig بدون قاعده تا بررسی‌های قالب‌بندی روی کد موجود پاس بمانند)، DA-B08/B09 ‏(.gitignore) و DA-D04 ‏(.dockerignore) را اصلاح می‌کند و بقیه را دستی فهرست می‌کند. قاعده‌های ساختاری اختیاری که در `--rules` نام برده می‌شوند: ‏DA-B03 ‏(نسخه‌های مرکزی پکیج؛ pinهای متفاوت با `VersionOverride` می‌مانند و نسخه‌های resolve‌شده تغییر نمی‌کنند)، DA-B07 ‏(هشدار در CI/Release خطا باشد)، DA-S04 ‏(پروژهٔ تست Domain با تست معماری، با `dotnet sln add` ثبت می‌شود)، DA-S06 ‏(layout v2: پروژه‌ها به `src/` و تست‌ها به `tests/`؛ solution، ارجاع پروژه‌ها، مسیرهای Dockerfile/compose/CI/.corevia، مسیرهای README/spec و path literalها در کد بازنویسی می‌شوند؛ فایل‌های تاریخی مثل changelog و evidence دست‌نخورده می‌مانند). جابه‌جایی یک تغییر طرح‌ریزی‌شده با action به نام `move` است.

هم‌ترازی با ABP (layout v3، `abp-alignment.fa.md`): ‏`project.yml` کلیدهای `standards: [abp]` ‏(مجموعه‌قاعدهٔ داخلی و اختیاری DA-A01..DA-A11؛ ‏`profile.yml` هم می‌تواند `standards` و نگاشت `severity` داشته باشد) و `layout: v3` ‏(`src/`، `test/`، `etc/`) را می‌گیرد. ‏`fix` قاعده‌های اختیاری DA-A01 (از v2 به v3: `tests/` به `test/`) و DA-A02 را می‌افزاید: لایه‌ها بر اساس محتوا ساخته می‌شوند؛ فایل‌های enum، DTO خالص و requestهای MediatR به `Domain.Shared` یا `Application.Contracts` می‌روند، اما mapperهای entity در Application می‌مانند چون به Domain وابسته‌اند؛ namespace و مسیر درون پروژه ثابت می‌ماند و ارجاع‌ها، بسته‌ها و مرحلهٔ restore در Dockerfile وصل می‌شوند. ‏`--empty` لایهٔ خالی از جمله `HttpApi.Client` هم می‌سازد و موارد جابه‌جایی‌ناپذیر را دستی فهرست می‌کند. ‏DA-A08 کلاینت تایپ‌دار را از controllerها می‌سازد و DA-A09 فایل‌های Compose ریشه را به `etc/docker/` می‌برد. ‏`new solution --layout=v3` فقط لایه‌های دارای محتوا را می‌سازد.
‏`rules.yml`: ‏`severity` ‏(شناسه به off|info|warning|error)، `thresholds` ‏(`coverage_line`)، `exceptions` ‏(`rule`، `reason` اجباری، `path` اختیاری). ‏`profile.yml`: ‏`name`، `version`، `source` اختیاری، `accepted_layouts`، `rules[]` با انواع `require-files`، `forbid-project-reference`، `dockerfile-forbid-line`، `folder-prefix`، `note-if-files-match`.
شناسه‌های جدید doctor: ‏DA-M06..M09 ‏(مستندات)، DA-T01..T02 ‏(تست و پوشش)، DA-BP01..BP05 ‏(blueprint سرویس: وجود، خواندنی و نسخه‌دار بودن، تولیدشدن، توصیف‌شدن، اعمال‌شدن).

transport کلاینت تایپ‌دار (`profile.yml`): به‌طور پیش‌فرض کلاینت تولیدشده `HttpClient` می‌گیرد. پروفایل می‌تواند به‌جای آن یک انتزاع REST client که string برمی‌گرداند را نام ببرد: `client: { transport: rest-client, interface: <نام>, namespace: <namespace>, package: <پکیج NuGet> }`. انتزاع باید این شکل را داشته باشد: `Task<string> GetAsync(string path, IDictionary<string,string>? headers, IDictionary<string,string?>? query, CancellationToken ct)`، ‏`PostAsync/PutAsync/PatchAsync(string path, object? body, headers, query, ct)` و `DeleteAsync(path, headers, query, ct)`؛ کلاینت تولیدشده آن را در constructor می‌گیرد، JSON برگشتی را با `System.Text.Json` (پیش‌فرض‌های web) می‌خواند، query string را داخل path می‌گذارد تا کلیدهای تکراری حفظ شود و پروژهٔ کلاینت به پکیج نام‌برده ارجاع می‌دهد. پاسخ بایت خام با آن قابل‌بیان نیست و دستی فهرست می‌شود. ابزار نام هیچ سازمان یا محصولی را نمی‌داند؛ نام‌ها را پروفایل می‌دهد.
