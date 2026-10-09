# مرور DotNetArch

DotNetArch یک ابزار گلوبال چندسکویی دات‌نت (`dotnet-arch`) است که میکروسرویس‌های Clean Architecture و Kitهای مستقل مبتنی بر provider تولید می‌کند و خودش را به‌عنوان سرور MCP برای agentها در دسترس می‌گذارد.
ساختار آن سه لایه است: Cli (دستورها)، Mcp (سرور ابزار) و Core (پیاده‌سازی).

## اهداف
۱. تولید میکروسرویس در سطح تولید (Clean Architecture، ports/adapters، vertical slice و CQRS و UoW و repository، Docker، Git، CI، تست، host اختیاری MCP).
۲. تولید Kit مستقل مبتنی بر provider برای قابلیت‌های خارجی (Cache، MessageBroker، MediaStorage ...).
۳. قابل استفاده برای agentها از طریق MCP.
۴. spec-driven و agent-driven: specها و AGENTS.md بر هر تغییر حاکم‌اند، هم در این مخزن و هم در هرچه تولید می‌کند.

## غیراهداف
میزبانی runtime، میزبانی registry، راستی‌آزمایی زنده provider kitها با سرور واقعی (با stub تأیید می‌شوند)، نصب .NET SDK.

## نقشهٔ اسناد
`requirements.md` (خواسته‌ها) - `decisions/decisions.md` (چرا) - `architecture.md` (درخت‌ها) - `contracts.md` (CLI، پیکربندی، MCP) - `acceptance.md` (راستی‌آزمایی) -
`abp-alignment.md` (استانداردهای ABP و فاصله از آن) - `changelog.md` - `../ROADMAP.md` (فازها و نقطهٔ ادامه) - `openspec.yaml` و `testspec.yaml` (ماشین‌خوان). نسخه‌های فارسی `.fa.md` هستند.

## مراجع
`samples/corevia-identity` (شکل میکروسرویس، با ایرادهای ثبت‌شده در D-10) و `samples/MediaStorage` (شکل kit)؛ هر دو فقط‌خواندنی‌اند.
