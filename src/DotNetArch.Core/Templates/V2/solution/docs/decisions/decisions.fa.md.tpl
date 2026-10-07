# دفتر تصمیم‌ها

| شناسه | تصمیم | دلیل |
| --- | --- | --- |
| D-01 | معماری Clean با ports و adapters و لایه‌های جدا | تست‌پذیری، جایگزینی زیرساخت |
| D-02 | vertical slice به‌ازای هر entity با CQRS ‏(MediatR 12.x با مجوز Apache-2.0) | انسجام، handlerهای کوچک |
| D-03 | unit of work و repository روی EF Core؛ async و بدون `IQueryable` در پورت‌ها | ذخیره‌سازی aggregate بدون نشت ORM |
| D-04 | appsettings برای مقادیر غیرحساس، environment/`.env` برای secret، یک مرحلهٔ بارگذاری | سبک twelve-factor، پیش‌فرض‌های امن |
| D-05 | قابلیت‌های خارجی به‌صورت kit مستقل (Abstractions / Core / Providers) | تعویض provider با پیکربندی، نسخه‌بندی جدا |

هر تصمیم جدید را به‌صورت یک ردیف اضافه کنید؛ تاریخچه را بازنویسی نکنید، با ردیف جدید جایگزین کنید.
