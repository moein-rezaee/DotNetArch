# تغییرات

## منتشرنشده
- دستور جدید `dotnet-arch doctor` (تشخیص فقط‌خواندنی: لایه‌ها، جهت وابستگی، تست و پوشش، بهداشت build، پیکربندی و secretها، Docker/CI، مستندات، قواعد کد، مرز kitها؛ خروجی متن یا `--json`؛ کد خروج ۳ برای حالت مسدودکننده).
- دستور جدید `dotnet-arch adopt` ‏(پروژهٔ موجود را فقط با نوشتن `.net-arch/` تحت کنترل می‌آورد) و `dotnet-arch fix` ‏(اصلاحات مکانیکی بهداشتی، اول طرح، با `--apply` می‌نویسد).
- پوشهٔ جدید `.net-arch/` ‏(`project.yml`، `rules.yml`، `profile.yml`، `generated.lock`) با `~/.net-arch/` سراسری؛ rules.yml تغییر شدت، آستانه‌ها و استثنا با دلیل را نگه می‌دارد.
- قواعد سازمانی یک پروفایل اعلانی است (`profile.yml`)؛ خود ابزار هیچ بررسی مخصوص سازمان ندارد.
- یک رجیستری عملیات هم CLI و هم سرور MCP را تغذیه می‌کند (۳ ابزار رجیستری: `doctor`، `adopt`، `fix`؛ در مجموع ۱۸ ابزار).
- doctor: اعتبارسنجی مستندات (فهرست، specها، snapshot ‏OpenAPI) و بررسی تست (وجود تست، پوشش در برابر آستانه).

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
