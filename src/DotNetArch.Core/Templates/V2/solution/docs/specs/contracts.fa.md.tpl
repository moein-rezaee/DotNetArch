# {{App}} - قراردادها

## API ‏HTTP
مسیر پایه `/api`. خطاها با problem details ‏(RFC 7807): ‏400 اعتبارسنجی/قانون کسب‌وکار، 404 پیدا نشد، 500 غیرمنتظره.

جدول entityها و routeها در نسخهٔ انگلیسی `contracts.md` به‌صورت خودکار نگهداری می‌شود.

## پیکربندی
| کلید | نوع | محل |
| --- | --- | --- |
| `Database:Provider` | غیرحساس | appsettings |
| `Database:MigrateOnStartup` | غیرحساس | appsettings |
| `DATABASE_CONNECTION_STRING` | secret | environment / `.env` |
| `Cors:AllowedOrigins` | غیرحساس | appsettings |

کلیدهای kit را `dotnet-arch new kit` / `new service` اضافه می‌کنند؛ کلیدهای هر kit در README خودش است.

## رویدادها
رویدادهای دامنه توسط entityها ایجاد، توسط unit of work جمع و پس از commit موفق به‌صورت `DomainEventNotification<TEvent>` منتشر می‌شوند.
