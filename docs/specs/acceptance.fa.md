# معیارهای پذیرش

راستی‌آزمایی: تست‌های هر لایه در `tests/` (D-17) و `scripts/smoke.sh` که در پوشهٔ موقت تولید می‌کند، build می‌کند و تست می‌گیرد. تا اجرا نشده چیزی «تأییدشده» ادعا نمی‌شود.

- AC-1 (R-A1..A8) solution v2 build می‌شود؛ ارجاع پروژه‌ها با قاعدهٔ `architecture.md` می‌خواند؛ Application به Infrastructure یا Core/Provider ارجاع ندارد؛ هر لایه فقط پکیج‌های لازمش را دارد.
- AC-2 (R-A4) `new crud Product` پوشهٔ `Features/Products/{Commands,Queries,Dtos}/<Name>/` می‌سازد و `new action` زیر `Actions/<Name>/` همان entity اضافه می‌کند.
- AC-3 (R-A9,A10) secret فقط در env و مقدار غیرحساس فقط در appsettings هر دو در `IConfiguration` می‌آیند؛ فایل‌های example همهٔ کلیدها را دارند.
- AC-4 (R-A11,A12) `docker compose` برای Api معتبر است؛ git با commit اولیه مگر `--no-git`.
- AC-5 (R-A13) `<App>.Mcp` برای هر entity و action ابزار دارد که همان درخواست MediatR را می‌فرستد.
- AC-6 (R-A14) هر لایه پروژهٔ تست دارد؛ `dotnet test` روی solution تازه موفق است.
- AC-7 (R-A15) برای remoteهای github/gitlab/azure/bitbucket فایل CI مناسب تولید می‌شود؛ `--docker-registry` و `--nuget-source` بدون credential در NuGet.config و CI و compose می‌آیند.
- AC-8 (R-B1,B2) `new service` با منطق، سرویس Application می‌سازد؛ بدون منطق، kit.
- AC-9 (R-B3..B8) `new kit` ساختار Abstractions/Core/Providers می‌سازد، مستقل build و pack می‌شود و `add kit` فقط Abstractions را به Application و Core و Providerها را به composition root وصل می‌کند.
- AC-10 (R-C1) کلاینت MCP می‌تواند ابزارهای `mcp serve` را فهرست کند و solution و entity بسازد.
- AC-11 (R-D1..D5) مخزن ابزار، پروژه و kit تولیدشده AGENTS.md، specها و جفت README دارند؛ `scripts/check-docs.sh` موفق است.
- AC-12 (D-10) کد تولیدشده ایرادهای sample را ندارد (بدون `GetAwaiter().GetResult()`، بدون `IQueryable<T>` در پورت، بدون `AllowAnyOrigin`، بدون stack trace با Console).
- AC-13 (D-11) خروجی فقط با NuGet عمومی restore می‌شود.
- AC-14 (R-F1..F3) ‏`dotnet-arch doctor` روی solution تازه‌تولیدشدهٔ v2 خطایی گزارش نمی‌کند؛ روی ریپویی با ارجاع رو به بالا، secret در appsettings یا پکیج Core/Provider کیت در Application، خطاهای DA-S03 / DA-C05 / DA-K06 را گزارش و با کد `3` خارج می‌شود؛ `--json` و ابزار MCP به نام `doctor` همان findingها را برمی‌گردانند.
- AC-15 (R-G1,G2) ‏`adopt` بدون `--apply` چیزی نمی‌نویسد؛ با `--apply` فقط داخل `.net-arch/` می‌نویسد، hash هر فایل سورس ثابت می‌ماند، اجرای دوم ۰ تغییر طرح می‌دهد و حذف `.net-arch/` یافته‌های doctor را عوض نمی‌کند.
- AC-16 (R-G3) ‏`fix` فقط فایل‌های بهداشتی/خط‌های ignore گم‌شده را می‌سازد، هرگز کد را ویرایش نمی‌کند و در اجرای دوم ۰ تغییر طرح می‌دهد.
- AC-17 (R-G4,G5) هر عملیات رجیستری یک ابزار MCP با همان نام و توضیح است، ابزارهای تغییردهنده `apply` را اعلام می‌کنند و شدت/استثنای rules.yml نتیجهٔ doctor را عوض می‌کند (استثناهای پذیرفته‌شده فهرست می‌شوند، پنهان نمی‌شوند).
- AC-18 (R-G3) قاعده‌های ساختاری `fix` اختیاری‌اند: بدون `--rules` فقط به‌صورت دستی فهرست می‌شوند؛ DA-B03 همهٔ نسخه‌های resolve‌شدهٔ پکیج را نگه می‌دارد، DA-S04 با نسخه‌های مرکزی پروژهٔ تست Domain را بدون نسخهٔ درون‌خطی می‌سازد، و DA-S06 پروژه‌ها را به `src/` و تست‌ها را به `tests/` می‌برد، هر مسیر اشاره‌کننده به آن‌ها را بازنویسی می‌کند، اسناد تاریخی را دست‌نخورده می‌گذارد و در اجرای دوم ۰ تغییر طرح می‌دهد.
- AC-19 (R-H1) ‏`fix --rules=DA-A01` پوشهٔ `tests/` را به `test/` تغییر می‌دهد، solution و ارجاع پروژه‌ها و مسیرهای Dockerfile/CI/compose و literalهای مسیر در کد را بازنویسی می‌کند، سندهای تاریخچه را دست نمی‌زند، همهٔ تست‌ها پاس می‌مانند و اجرای دوم ۰ تغییر طرح می‌کند.
- AC-20 (R-H2) ‏`fix --rules=DA-A02` پروژهٔ لایه را فقط همراه فایل‌هایی می‌سازد که به آن می‌روند، هر فایل جابه‌جاشده بایت‌به‌بایت همان می‌ماند، ارجاع‌ها و پکیج‌ها و `InternalsVisibleTo` و خطوط restore در Dockerfile وصل می‌شود، پروژه‌ها در solution ثبت می‌شوند، لایهٔ خالی ساخته نمی‌شود (مگر با `--empty`)، اگر یک controller نتواند برود همه می‌مانند و اجرای دوم ۰ تغییر طرح می‌کند. یک سرویس واقعی (Catalog) بعد از جابه‌جایی بدون تغییر build می‌شود و تست‌هایش پاس می‌شود.
- AC-21 (R-H3,H5) با `standards: [abp]` ‏`doctor` ‏DA-A01..DA-A11 را با مکان و راهنمای رفع گزارش می‌کند؛ بدون آن هیچ‌کدام ظاهر نمی‌شود؛ استثنا در `rules.yml` یافته را به فهرست پذیرفته‌شده می‌برد.
- AC-22 (R-H4) راه‌حل تولیدشده با `--layout=v3` build می‌شود، تست‌هایش پاس است و `doctor` با `standards: [abp]` خطای DA-A ندارد.
- AC-23 (R-H6) ‏`fix --rules=DA-A08` برای هر controller یک رابط و یک کلاس با route، verb، پارامترهای route/query/body و نوع پاسخ هر اکشنِ قابل‌بیان می‌سازد، بقیه را با دلیل دستی فهرست می‌کند، فقط به Application.Contracts ارجاع می‌دهد، کامپایل می‌شود و اجرای دوم ۰ تغییر طرح می‌کند. روی Catalog هر ۴۵ route ثبت‌شده را می‌پوشاند.
- AC-24 (R-H7) ‏`fix --rules=DA-A09` فایل Compose ریشه را به `etc/docker/` می‌برد، build context و env file و bind mountها را بازپایه می‌کند تا `docker compose -f etc/docker/<file> config` همان فایل‌ها را پیدا کند، سندهای تاریخچه را دست نمی‌زند و اجرای دوم ۰ تغییر طرح می‌کند.
- AC-25 (R-H8) ‏`doctor` با DA-A10 فایل رهاشده در ریشهٔ پروژه و پوشه با بیش از دوازده فایل را گزارش می‌کند؛ ‏`fix --rules=DA-A10` آن‌ها را با namespace دست‌نخورده به پوشه‌های نوع/فیچر در همان پروژه می‌برد، فایل‌های ترکیبی مانند `AssemblyMarker.cs` را نگه می‌دارد و اجرای دوم ۰ تغییر طرح می‌کند.
- AC-26 (R-H9) ‏`doctor` با DA-A11 ارجاع گمشده، ارجاع ممنوعِ بی‌استفاده، پروژهٔ بیرون از solution و خط restore گمشدهٔ Dockerfile را گزارش می‌کند؛ ‏`fix --rules=DA-A11` درستشان می‌کند.
- AC-27 (R-I1) رجیستری همهٔ دستورهای مستند را فهرست می‌کند؛ هر دستور با کلمه‌هایش از CLI در دسترس است و یک ابزار MCP با همان نام و توضیح است؛ هیچ ابزاری destructive نیست؛ ابزارهای تغییردهنده `apply` دارند.
- AC-28 (R-I1) ‏`new solution` بدون apply چیزی نمی‌نویسد و فایل‌ها را فهرست می‌کند؛ با apply می‌سازد. ‏`new crud` بدون apply solution را تغییر نمی‌دهد.
- AC-29 (R-I3,I4) ‏solution ساخته‌شده با layout v3 و `new crud`، `new action` و `new event` بدون هشدار build می‌شود، تست‌هایش پاس است، پروژه‌های لایهٔ ABP و کلاینت تایپ‌دار دارد و `doctor` هیچ یافتهٔ DA-A گزارش نمی‌کند.
- AC-30 (R-I2) ‏`add_layer` لایه را فقط وقتی چیزی به آن منتقل می‌شود می‌سازد و `add_tests` پروژهٔ تست ثبت‌شده اضافه می‌کند؛ schema ابزار MCP ‏`add_layer` پارامتر `empty` ندارد.
- AC-31 (R-I5) بعد از `new crud` و سپس `new action` در solution با layout v3 کلاینت action تازه را دارد، پروژهٔ تست کلاینت (با تست برابری route) پاس است و اجرای دوم `fix --rules=DA-A08` ۰ تغییر طرح می‌کند.
