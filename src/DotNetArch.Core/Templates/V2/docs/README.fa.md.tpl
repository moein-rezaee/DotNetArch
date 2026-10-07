[English](./README.md)

# {{App}}

تولیدشده با [DotNetArch](https://github.com/moein-rezaee/DotNetArch) (layout v2): معماری Clean، ports و adapters، vertical slice با CQRS ‏(MediatR)، unit of work و repository.

## ساختار
```text
src/
  {{App}}.Domain           entityها، value objectها و رویدادهای دامنه (بدون ارجاع)
  {{App}}.Application      پورت‌ها، features/<entities>/{Commands,Queries,Actions,Events,Dtos}، validatorها
  {{App}}.Infrastructure   EF Core، repository، unit of work، adapterها، بارگذاری پیکربندی
  {{App}}.Api              controllerها، composition root
kits/                      kitهای مستقل (cache، message broker، media storage، ...)
tests/                     تست هر لایه
```
وابستگی‌ها به داخل است: `Api -> Infrastructure -> Application -> Domain`. هر لایه سرویس‌های خودش را ثبت می‌کند (`AddApplication`، `AddInfrastructure`، `AddApi`).

## اجرا
```bash
cp src/{{App}}.Api/.env.example src/{{App}}.Api/.env   # secretها و مقادیر runtime
dotnet run --project src/{{App}}.Api
```

## پیکربندی
- `appsettings.json` مقادیر غیرحساس؛ `.env` و environment برای secretها (`DATABASE_CONNECTION_STRING`). environment واقعی بر `.env` غلبه دارد.
- `.env.example` و `appsettings.example.json` را با کد همگام نگه دارید (تست‌های `Category=Configuration`).

## تولید کد بیشتر
```bash
dotnet-arch new crud --entity=Product
dotnet-arch new action --entity=Product --method=POST --action=Archive
dotnet-arch new service
```
مستندات و specها در `docs/` و قواعد در `AGENTS.md` هستند.
