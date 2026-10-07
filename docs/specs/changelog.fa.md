# تغییرات

## منتشرنشده
- هنوز چیزی نیست.

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
