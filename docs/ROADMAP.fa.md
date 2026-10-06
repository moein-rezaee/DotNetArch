# Roadmap (نسخهٔ فارسی)

مرجع اصلی با تیک‌ها: `ROADMAP.md`. این فایل خلاصهٔ هم‌شناسه است و در هر commit با آن همگام می‌شود.

## نقطهٔ ادامه
فاز فعلی: ۱ (بازساخت Core/Cli انجام شد؛ مانده: ۱.۸، ۱.۹b، ۱.۱۰، ۱.۱۱b، ۱.۱۲). مورد بعدی: ۱.۸ سپس ۱.۱۰ (تست‌ها). branch: `claude/exciting-fermi-b41fue`.
محیط: dotnet SDK 8 نصب شد (SDK 9 با apt در دسترس نیست؛ net9 باید در CI بررسی شود). باگ csproj که `samples/**` را compile می‌کرد رفع شد.

## فازها
- **۰ مستندات:** requirements، decisions، معماری و درخت‌ها، contracts، acceptance، AGENTS.md، roadmap ✔؛ بعدی: openspec/testspec، تلاش نصب SDK.
- **۱ بازساخت ابزار به Cli / Mcp / Core و سخت‌سازی:** رفع crash اسپینر در حالت غیر TTY، snapshot پایه (golden)، اسکلت `src/` و `tests/`، `ToolHost` و انتزاع‌های prompt/process/output، انتقال به Core، شکستن `Main.cs` به Commands، ProcessRunner بدون shell، اعتبارسنجی نام‌ها، `MigrationRunner`، تست‌های Core و Cli، `scripts/smoke.sh`.
- **۲ قالب میکروسرویس v2:** `layout: v2`، اسکلت `src/`+`tests/`، Domain با رفتار، Application (پورت‌ها، MediatR، FluentValidation)، Infrastructure (EF، repository async، UoW)، Api (Program کوتاه، controller به‌ازای entity)، دستورهای crud/action/event/enum/constant تودرتو.
- **۳ پیکربندی:** `AddAppConfiguration()`، options با validation، تولید فایل‌های example و اسکریپت اعتبارسنجی.
- **۴ Docker و Git و CI و registry:** Dockerfile/compose، git با host شخصی، تشخیص CI و قالب‌های GitHub/GitLab/Azure/Bitbucket، registry شخصی Docker و NuGet، دستورهای `ci add`، `docker add`، `git setup`.
- **۵ تست در پروژهٔ تولیدشده:** پروژهٔ تست هر لایه، job تست در CI، `testspec.yaml`.
- **۶ Kitها:** scaffolder کیت (Abstractions/Core/Providers + docs)، recipeهای MediaStorage و Cache و MessageBroker، `new service` با پرسش منطق کسب‌وکار، `add kit`، jobهای CI و انتشار در NuGet شخصی.
- **۷ MCP:** `mcp serve` و tools؛ host ‏`<App>.Mcp` برای پروژه.
- **۸ مستندات نهایی:** README، قالب docs/specs/AGENTS پروژه و kit، mirrorهای فارسی، اسکریپت بررسی جفت‌های دو زبانه.
- **۹ راستی‌آزمایی و انتشار:** اجرای smoke (نیاز به SDK)، نسخه و changelog.
