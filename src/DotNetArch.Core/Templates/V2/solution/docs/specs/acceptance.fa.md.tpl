# {{App}} - پذیرش

- الف۱. `dotnet build -c Release` بدون warning است و `dotnet test` روی clone تمیز موفق است.
- الف۲. هر use case با status codeهای `contracts.md` پاسخ می‌دهد؛ خطاهای اعتبارسنجی و قانون کسب‌وکار problem details هستند نه stack trace.
- الف۳. سرویس فقط با `DATABASE_CONNECTION_STRING` (و secretهای kitهای مصرفی) بالا می‌آید؛ نبودن secret الزامی هنگام شروع با ذکر نام کلید خطا می‌دهد.
- الف۴. `.env.example` و `appsettings.example.json` با کد همخوان‌اند (`scripts/validate-examples.sh`).
- الف۵. قواعد لایه‌ها برقرار است: Domain ارجاعی ندارد؛ Application به Infrastructure، host یا Core/Provider یک kit ارجاع نمی‌دهد.
- الف۶. هر endpoint و ابزار MCP یک entity همان درخواست MediatR را اجرا می‌کند.
