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
- R-H1. layout v3 ‏(`src/`، `test/`، `etc/`) در `adopt`، `doctor` و `fix` پشتیبانی می‌شود؛ v2 معتبر می‌ماند؛ ‏`fix --rules=DA-S07` از v2 به v3 می‌برد و همهٔ مسیرهای اشاره‌کننده به پوشه‌های جابه‌جاشده را بازنویسی می‌کند.
- R-H2. پروژه‌های لایه ‏`Domain.Shared`، `Application.Contracts`، `HttpApi`، `HttpApi.Client` اختیاری‌اند؛ ‏`fix --rules=DA-S08` آن‌ها را فقط با ارجاع‌های مجاز می‌سازد و هرگز نوعی را جابه‌جا نمی‌کند.
- R-H3. ‏`doctor` مجموعه‌قاعدهٔ داخلی و اختیاری `abp` ‏(`DA-A01..DA-A06`) دارد که با `standards: [abp]` فعال می‌شود؛ پروفایل می‌تواند الزامش کند و استثناهای پذیرفته‌شده را با دلیل فهرست کند.
- R-H4. ‏`new solution --layout=v3` درخت هم‌شکل ABP را تولید می‌کند (اختیاراً با پروژه‌های لایه) و مجموعه‌قاعدهٔ `abp` را می‌گذراند.
- R-H5. ابزار قاعده‌ای دربارهٔ idهای `Guid`، application service یا قابلیت‌های زمان اجرای ABP ندارد.
