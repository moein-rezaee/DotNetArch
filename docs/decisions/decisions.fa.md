# دفتر تصمیم‌ها

نسخهٔ انگلیسی: `decisions.md` (شناسه‌ها یکسان). **مالک** = تصمیم صاحب پروژه. **پیشنهادی** = تصمیم من بر اساس قواعد مالک، قابل تغییر.

## D-01 دو حالت MCP (مالک)
(الف) `dotnet-arch mcp serve` برای استفاده از خود ابزار/کتابخانه توسط agent؛ (ب) host ‏MCP در پروژهٔ تولیدشده که tools آن از طریق همان command/query های MediatR با controller و service ها هم‌سو است.

## D-02 لایهٔ تست فقط در صورت نیاز (مالک، با اصلاح D-17)
پروژهٔ تولیدشده برای هر لایه تست دارد؛ kit به‌صورت پیش‌فرض ندارد (`--with-tests`)؛ خود ابزار فقط برای لایه‌هایی که نیاز دارند (D-17). `scripts/smoke.sh` بررسی انتها‌به‌انتها باقی می‌ماند.

## D-03 مرجع‌ها (مالک)
میکروسرویس مرجع `samples/corevia-identity` و kit مرجع `samples/MediaStorage`؛ فقط خواندنی.

## D-04 MediatR (مالک)
برای CQRS از MediatR، نسخهٔ 12.x (Apache-2.0) و نسخه فقط در `Directory.Packages.props`.

## D-05 پوشهٔ kits (مالک)
`kits/<Area>/<Prefix>.Kit.<Area>.{Abstractions,Core,Providers.<Name>}` با `Directory.Build.props`، نسخه، job در CI و docs/specs مستقل.

## D-06 انتخاب خودکار CI (مالک)
تشخیص از host مربوط به `git remote origin`: github → GitHub Actions، gitlab → GitLab CI، azure → Azure Pipelines، bitbucket → Bitbucket Pipelines؛ ناشناخته → یک بار پرسش و ذخیره در `dotnet-arch.yml`. `--ci` می‌تواند override کند. git شخصی: `--git-host` و `--git-provider`.

## D-07 registry شخصی (مالک)
`--docker-registry` و `--nuget-source`؛ در `dotnet-arch.yml`، `NuGet.config`، متغیرهای CI، نام image در compose و jobهای انتشار kit. هیچ credential داخل فایل‌ها نوشته نمی‌شود، فقط نام متغیر.

## D-08 `new service` (مالک)
حالت داخلی بدون تغییر؛ حالت خارجی = تولید Kit. پرسش: «آیا این سرویس منطق کسب‌وکار دارد؟»

## D-09 مدل پیکربندی (مالک)
env = secret و مقادیر runtime (کلید UPPER_CASE)، appsettings = غیرحساس؛ هر دو در یک مرحلهٔ `AddAppConfiguration()` قبل از هر options binding. فایل‌های example اجباری و با اسکریپت در CI اعتبارسنجی می‌شوند.

## D-10 انحراف از sample (قاعدهٔ مالک + یافته‌های بازبینی)
کد تولیدشده این موارد را کپی نمی‌کند: sync-over-async؛ `IQueryable.Any()` همگام در handler؛ نشت `IQueryable` از `IRepository`؛ دامین بی‌رفتار با setter عمومی؛ پورت‌های غیر دامینی در `Domain`؛ پیاده‌سازی در Application؛ زیرپوشه‌های ناهمگون feature؛ controller های تخت؛ ارجاع EF Design در Api؛ `Program.cs` طولانی؛ چاپ stack trace با Console و CORS باز.

## D-11 بدون وابستگی خصوصی Corevia (پیشنهادی)
خروجی فقط با NuGet عمومی (و registry خود کاربر) build شود؛ هر kit exception های سبک خودش را دارد.

## D-12 چیدمان جدید با نسخهٔ قالب (پیشنهادی)
پیش‌فرض جدید `src/` و `tests/` با `layout: v2`؛ نبودن کلید یعنی چیدمان قدیمی و دستورهای قدیمی روی آن کار می‌کنند.

## D-13 یک پکیج نصب‌شدنی (پیشنهادی؛ لایه‌بندی با D-17 جایگزین شد)
یک ابزار گلوبال `dotnet-arch` و MCP زیردستور `mcp serve` است.

## D-14 پیشوند kit (پیشنهادی)
`--kit-prefix` یا نام solution/سازمان؛ نام پکیج `<Prefix>.Kit.<Area>.<Part>`؛ بدون پیشوند ثابت شرکتی در ابزار.

## D-15 فریم‌ورک (پیشنهادی)
net8.0 یا net9.0 به انتخاب کاربر؛ `global.json` با `rollForward: latestFeature`.

## D-16 فرایند (مالک)
اول docs/spec/قواعد/roadmap؛ سپس فاز به فاز با commit و push؛ چک‌باکس‌های roadmap حالت ادامه‌اند.

## D-17 ساختار استاندارد چندلایهٔ ابزار (مالک)
`src/DotNetArch.Cli` (لایهٔ دستور: پارس آرگومان، prompt، خروجی کنسول، بسته‌بندی ابزار گلوبال)، `src/DotNetArch.Mcp` (لایهٔ MCP: tools که Core را با host غیرتعاملی صدا می‌زنند)، `src/DotNetArch.Core` (کل پیاده‌سازی: config، scaffolding، قالب‌ها، انتزاع فرایند/فایل). وابستگی: `Cli -> Core`، `Mcp -> Core`، `Cli -> Mcp` فقط برای `mcp serve`؛ Core به هیچ‌کدام ارجاع نمی‌دهد. برای هر لایهٔ نیازمند، پروژهٔ تست در `tests/` (`Core.Tests`، `Cli.Tests`، `Mcp.Tests`).

## D-18 انتزاع Host برای Core (پیشنهادی)
Core مستقیم به `Console` و shell دست نمی‌زند؛ از `ToolHost` شامل `IPrompter`، `IProcessRunner`، `IToolOutput` استفاده می‌کند. Cli پیاده‌سازی کنسولی، Mcp پیاده‌سازی غیرتعاملی و تست‌ها fake نصب می‌کنند. scaffolderها فعلاً static می‌مانند؛ تبدیل به instance تزریقی در roadmap (۱.۹).

