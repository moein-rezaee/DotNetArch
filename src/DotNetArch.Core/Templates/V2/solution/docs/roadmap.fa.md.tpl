# Roadmap

آیتم‌ها را پس از انجام تیک بزنید و «نقطهٔ ادامه» را دقیق نگه دارید تا پس از هر وقفه بتوان ادامه داد.

## نقطهٔ ادامه
بعدی: تعریف entityها و use caseهای اول.

## فاز ۱ - پایه
- [x] تولید solution با DotNetArch (لایه‌ها، پیکربندی، تست‌ها)
- [ ] توصیف سرویس در `docs/specs/overview.md`
- [ ] تولید entityهای اول (`dotnet-arch new crud --entity=...`)

## فاز ۲ - قابلیت‌ها
- [ ] قابلیت‌های خارجی به‌صورت kit (`dotnet-arch new service`)
- [ ] رویدادهای دامنه و subscriberها (`dotnet-arch new event`)

## فاز ۳ - انتشار
- [ ] CI سبز، image کانتینر ساخته شده، فایل‌های example پیکربندی اعتبارسنجی شده
