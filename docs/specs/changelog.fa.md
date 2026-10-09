# تغییرات

## منتشرنشده
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
