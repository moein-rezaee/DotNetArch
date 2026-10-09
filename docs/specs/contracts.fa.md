# قراردادها (CLI، پیکربندی، MCP)

## CLI
دستورها (همه عملیات رجیستری‌اند، D-32؛ دستور دوکلمه‌ای همان عملیات با `_` است: ‏`new crud` = `new_crud`): ‏`new solution`، `new crud`، `new action`، `new event`، `new enum`، `new constant`، `new service`، `new kit`، `add kit`، `add mcp`، `add layer`، `add tests`،
‏`ci add`، `docker add`، `git setup`، `spec list`، `spec add`، `spec check`، `graph`، `list entities`، `describe config`، `exec`، `remove migration`، `doctor`، `adopt`، `fix`، `mcp serve`.
هر دستوری که فایل تغییر می‌دهد اول طرح می‌دهد و فقط با `--apply` می‌نویسد؛ در ترمینال طرح نشان داده می‌شود و از کاربر پرسیده می‌شود. ‏`--json` خروجی `{ ok, operation, applied, error?, plan[], data }` را چاپ می‌کند و `--out=file` آن را ذخیره می‌کند. ‏`--path` (معادل `--output`) پوشهٔ solution است؛ مقدارها را می‌توان `--no-migration` یا `--no_migration` نوشت. مقدار الزامیِ نداده‌شده پرسیده می‌شود.

گزینه‌های `new solution <Name>`: `--output`، `--database=SQLite|SqlServer|Postgres`، `--style=controller|fast`، `--layout=v2|legacy` (پیش‌فرض v2)، `--mcp`،
`--ci=auto|none|github|gitlab|azure|bitbucket|gitea`، `--git-remote`، `--git-provider`، `--git-host`، `--docker-registry`، `--nuget-source`، `--nuget-source-name`،
`--no-docker`، `--no-git`، `--no-tests`. فریم‌ورک هدف از بالاترین SDK نصب‌شده (۸ یا ۹) گرفته می‌شود.

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
`dotnet-arch mcp serve` (stdio). هر عملیات رجیستری یک ابزار با همان نام، توضیح و مقدارهای دستور CLI است (`new_solution`، `new_crud`، `new_action`، `new_event`، `new_enum`، `new_constant`، `new_service`، `new_kit`، `add_kit`، `add_mcp`، `add_layer`، `add_tests`، `ci_add`، `docker_add`، `git_setup`، `spec_list`، `spec_add`، `spec_check`، `graph`، `list_entities`، `describe_config`، `exec`، `remove_migration`، `doctor`، `adopt`، `fix`). غیرتعاملی و بدون shell. ابزارهای فقط‌خواندنی علامت read-only دارند؛ هر ابزار تغییردهنده پرچم `apply` دارد (پیش‌فرض false: فقط طرح) و `{ ok, operation, applied, error?, plan[], data }` برمی‌گرداند. پارامترهای فقط‌CLI (مثلاً `empty` در `add_layer`) ارائه نمی‌شوند. هیچ ابزاری destructive نیست؛ ‏`remove_migration` و `exec` فقط با `apply: true` عمل می‌کنند.

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

هم‌ترازی با ABP (layout v3، `abp-alignment.fa.md`): ‏`project.yml` کلیدهای `standards: [abp]` ‏(مجموعه‌قاعدهٔ داخلی و اختیاری DA-A01..DA-A09؛ ‏`profile.yml` هم می‌تواند `standards` و نگاشت `severity` برای بالا یا پایین‌بردن شدت هر شناسه داشته باشد، با اولویت پایین‌تر از rules.yml) و `layout: v3` ‏(`src/`، `test/`، `etc/`) را می‌گیرد. ‏`fix` قاعده‌های اختیاری DA-A01 ‏(از v2 به v3: ‏`tests/` به `test/` با همان بازنویسی مسیر DA-S06) و DA-A02 ‏(لایه‌های مبتنی بر محتوا: ساخت `Domain.Shared`، `Application.Contracts` و `HttpApi` فقط جایی که فایل‌هایی به آن می‌روند - فایل‌های فقط-enum، DTO و requestهای MediatR، controllerها - با namespace و مسیر داخل پروژهٔ ثابت، وصل‌کردن ارجاع‌ها و پکیج‌ها و مرحلهٔ restore در Dockerfile؛ ‏`--empty` لایهٔ خالی از جمله `HttpApi.Client` هم می‌سازد؛ آنچه نمی‌تواند برود دستی فهرست می‌شود) را می‌افزاید. ‏DA-A08 پروژهٔ کلاینت تایپ‌دار (`HttpApi.Client`) را از روی controllerها تولید می‌کند (کد جدید، فقط نوع‌های contract؛ اکشن‌های پشتیبانی‌نشده دستی) و DA-A09 فایل‌های Compose ریشه را با مسیرهای بازپایه‌شده به `etc/docker/` می‌برد. ‏`new solution --layout=v3` درخت v3 را تولید می‌کند و فقط لایه‌هایی را تولید می‌کند که محتوا دارند.
‏`rules.yml`: ‏`severity` ‏(شناسه به off|info|warning|error)، `thresholds` ‏(`coverage_line`)، `exceptions` ‏(`rule`، `reason` اجباری، `path` اختیاری). ‏`profile.yml`: ‏`name`، `version`، `source` اختیاری، `accepted_layouts`، `rules[]` با انواع `require-files`، `forbid-project-reference`، `dockerfile-forbid-line`، `folder-prefix`، `note-if-files-match`.
شناسه‌های جدید doctor: ‏DA-M06..M09 ‏(مستندات)، DA-T01..T02 ‏(تست و پوشش).

transport کلاینت تایپ‌دار (`profile.yml`): به‌طور پیش‌فرض کلاینت تولیدشده `HttpClient` می‌گیرد. پروفایل می‌تواند به‌جای آن یک انتزاع REST client که string برمی‌گرداند را نام ببرد: `client: { transport: rest-client, interface: <نام>, namespace: <namespace>, package: <پکیج NuGet> }`. انتزاع باید این شکل را داشته باشد: `Task<string> GetAsync(string path, IDictionary<string,string>? headers, IDictionary<string,string?>? query, CancellationToken ct)`، ‏`PostAsync/PutAsync/PatchAsync(string path, object? body, headers, query, ct)` و `DeleteAsync(path, headers, query, ct)`؛ کلاینت تولیدشده آن را در constructor می‌گیرد، JSON برگشتی را با `System.Text.Json` (پیش‌فرض‌های web) می‌خواند، query string را داخل path می‌گذارد تا کلیدهای تکراری حفظ شود و پروژهٔ کلاینت به پکیج نام‌برده ارجاع می‌دهد. پاسخ بایت خام با آن قابل‌بیان نیست و دستی فهرست می‌شود. ابزار نام هیچ سازمان یا محصولی را نمی‌داند؛ نام‌ها را پروفایل می‌دهد.
