# قراردادها (CLI، پیکربندی، MCP)

## CLI
دستورها: `new solution`، `new crud`، `new action`، `new event`، `new enum`، `new constant`، `new service`، `new kit`، `add kit`، `add mcp`،
`ci add`، `docker add`، `git setup`، `exec`، `remove migration`، `mcp serve`.

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
`dotnet-arch mcp serve` (stdio). ابزارها غیرتعاملی و بدون shell‌اند: `new_solution`، `new_crud`، `new_action`، `new_event`، `new_enum`، `new_constant`، `new_service`، `new_kit`، `add_kit`، `add_mcp`،
`ci_add`، `docker_add`، `git_setup`، `list_entities`، `describe_config`. نتیجهٔ هر ابزار `{ ok, command, output, created[], modified[], error? }` است. هیچ ابزاری چیزی را حذف نمی‌کند.

## MCP: host تولیدشده
`src/<App>.Mcp` با streamable HTTP روی `/mcp` (stateless) و `/health` باز. احراز هویت: bearer token با مقایسهٔ constant-time با `MCP_AUTH_TOKEN` (حداقل ۲۴ نویسه).
ابزارها به‌ازای entity: `<entity>_list|get|create|update|delete` و به‌ازای action: `<entity>_<action>`؛ هر ابزار همان درخواست MediatR controller را می‌فرستد.

## کدهای خروج و خروجی
`0` موفق، `1` خطای استفاده/اعتبارسنجی، `2` نبودن .NET SDK. لاگ انسانی در CLI روی stdout و در `mcp serve` روی stderr می‌رود.

## دکتر
‏`dotnet-arch doctor [path] [--profile=auto|generic|corevia] [--json] [--strict] [--out=file]` و ابزار MCP به نام `doctor(repositoryPath, profile, json)`. فقط‌خواندنی: هیچ فایلی نوشته نمی‌شود (جز `--out`)، فرایندی اجرا نمی‌شود، .NET SDK لازم نیست.
Finding: ‏`{ id: "DA-<area><nn>", severity: error|warning|info, category, message, path?, hint?, details?[] }`. شناسه‌ها: S ساختار، B build، C پیکربندی، D کانتینر/CI، M مستندات، K کد، V پروفایل corevia.
کدهای خروج: `0` سالم، `1` خطای استفاده، `3` finding مسدودکننده.
