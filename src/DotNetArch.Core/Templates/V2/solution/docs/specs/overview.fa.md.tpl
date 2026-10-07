# {{App}} - مرور

{{App}} یک میکروسرویس تولیدشده با DotNetArch (layout v2) است.

## هدف
TODO: در دو جمله بنویسید این سرویس مالک چه چیزی است و چه کسی آن را صدا می‌زند.

## معماری
Clean Architecture با ports و adapters، vertical slice و CQRS ‏(MediatR)، unit of work و repository.
`Api (و Mcp) -> Infrastructure -> Application -> Domain`؛ هر لایه سرویس‌های خودش را ثبت می‌کند؛ kitها قابلیت‌های خارجی را فراهم می‌کنند.

## زمان اجرا
- پایگاه داده: {{Provider}} (EF Core). پیکربندی: appsettings (غیرحساس) و `.env`/environment (secret).
- نقاط ورود: API ‏HTTP (`src/{{App}}.Api`).

## خارج از دامنه
TODO.
