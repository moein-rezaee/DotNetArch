[English](./README.md)

# {{App}}

تولیدشده با [DotNetArch](https://github.com/moein-rezaee/DotNetArch) (layout v2): معماری Clean، ports و adapters، vertical slice با CQRS ‏(MediatR)، unit of work و repository.

## Purpose
در دو جمله بنویسید این سرویس مالک چه چیزی است و چه کسی آن را صدا می‌زند (جزئیات در `docs/specs/overview.fa.md`).

## Scope
فقط همین سرویس و ریپوی آن. سرویس‌های دیگر از راه پورت و آداپتور صدا زده می‌شوند.

## Prerequisites
.NET SDK (به `global.json` نگاه کنید)، یک پایگاه داده (`DATABASE_CONNECTION_STRING`) و پکیج‌های feed در `NuGet.config`.

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

## Configuration
- `appsettings.json` مقادیر غیرحساس؛ `.env` و environment برای secretها (`DATABASE_CONNECTION_STRING`). environment واقعی بر `.env` غلبه دارد.
- `.env.example` و `appsettings.example.json` را با کد همگام نگه دارید (تست‌های `Category=Configuration`).

## Run / Usage
```bash
cp src/{{App}}.Api/.env.example src/{{App}}.Api/.env   # secretها و مقادیر runtime
dotnet run --project src/{{App}}.Api
```

تولید کد بیشتر با ابزار:
```bash
dotnet-arch new crud --entity=Product
dotnet-arch new action --entity=Product --method=POST --action=Archive
dotnet-arch new service
```

## Validation / Verification
`dotnet build -c Release` بدون هشدار است، ‏`dotnet test` پاس است و `dotnet-arch doctor` یافته‌ای ندارد.

## Troubleshooting
`dotnet-arch doctor` قاعده و فایل را نام می‌برد؛ ‏`dotnet-arch fix` آنچه مکانیکی است را درست می‌کند. نبودن `DATABASE_CONNECTION_STRING` شروع را با پیامی که کلید را نام می‌برد متوقف می‌کند.

## Change Log
`docs/specs/changelog.fa.md` را ببینید.

## Ownership
تیمی که مالک این سرویس است (`.corevia/repo.yaml` یا `AGENTS.md`).
