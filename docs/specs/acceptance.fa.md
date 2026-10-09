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
