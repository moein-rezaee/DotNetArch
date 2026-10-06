[English](./identity-service-mcp-decision-log.md) | [Roadmap](../roadmaps/services/identity-service-mcp-roadmap.fa.md) | [اسپک Identity](../specs/README.fa.md)

> سند تاریخی منتقل‌شده از مونورپوی corevia-market در تاریخ 2026-10-04 (مرحله 5b از Phase C). مسیرها و دستورهایی مانند `IdentityService/...` و `corevia-market/...` چیدمان مونورپو را در زمان خود توصیف می‌کنند؛ در این مخزن همان پروژه‌ها در ریشه مخزن قرار دارند و مقادیر MCP زیر `.corevia/` هستند (`.corevia/mcp/identity.values.yaml`). مقادیر اجرای فاز که در اینجا ارجاع شده‌اند در corevia-market باقی می‌مانند.

# تصمیم‌های معماری MCP در IdentityService

## Purpose

این Decision Log تصمیم‌های معماری و امنیتی لازم برای Adapter sibling MCP در IdentityService را قبل از implementation و rollout ثبت می‌کند.

## Scope

- مرز Adapter MCP و وابستگی‌های Clean Architecture.
- Context هویت، Authentication، Authorization، Delegation، Policy مربوط به Tenant، Approval، Audit و Redaction.
- Toolهای Atomic/Business، Transport، استفاده Local/Cloud، استقرار روی سرور مشتری و Federation سازمانی آینده.

## Prerequisites

- قبل از تغییر Decision، [رودمپ MCP در IdentityService](../roadmaps/services/identity-service-mcp-roadmap.fa.md) و اسپک‌های IdentityService خوانده شوند.
- Outcome هر Decision فقط بعد از تایید صریح محصول/معماری ثبت شود.

## Configuration

- Secret، Token، Credential، داده مشتری یا Endpoint خصوصی Runtime در این Decision Log ثبت نشود.
- Evidence باید به Spec، Phase رودمپ، تست یا Report redact‌شده اشاره کند.

## Decision Records

### DEC-ISMCP-001 - Sibling Adapter و مرز Application

Status: Accepted

گزینه‌ها:

- **A: sibling adapter:** `IdentityService.Mcp` مستقیماً Use Caseهای Application/Handlerهای MediatR را فراخوانی کند و از مسیرهای Application به Policyهای Domain دسترسی داشته باشد.
- **B: HTTP Mirror:** MCP Controllerها یا HTTP API مربوط به Identity را صدا بزند و Toolهای endpoint-shaped expose کند.

تصمیم:

- گزینه A انتخاب شد.
- گزینه B رد شد، چون سطح Agent را به Routeهای HTTP وابسته می‌کند، Dump کردن Endpointها را تشویق می‌کند، Policy Tool را تضعیف می‌کند و ریسک Bypass/نشت Secret دارد.
- MCP نباید Controller، Endpoint HTTP، مسیر Gateway/Ocelot، EF Core، Repository، SQL یا `IdentityService.Api` را مستقیماً فراخوانی کند.
- Domain و Application باید از هر دو Adapter انتقال مستقل بمانند.

پیامد:

- پروژه MCP یک sibling واقعی است و به Contractهای روشن Application برای Context و Policy نیاز دارد.

### DEC-ISMCP-002 - Modeهای فعلی Deployment-neutral و Gateway اختیاری

Status: Accepted / Future Gateway Deferred

گزینه‌ها:

- **A: MCP مستقیم روی Local/Customer Server:** روی Local از `stdio` یا loopback امن و روی سرور مشتری از HTTPS احراز‌شده/HTTP شبکه خصوصی استفاده شود.
- **B: Gateway مرکزی اجباری:** همه درخواست‌های MCP از زیرساخت Corevia عبور کنند.

تصمیم:

- گزینه A برای رودمپ فعلی انتخاب شد.
- Corevia MCP Gateway مرکزی زیرساخت اختیاری آینده است و dependency هیچ نصب فعلی نیست.
- اگر در آینده ساخته شود، Gateway یک Relay/Policy/Routing Layer با Connector امن outbound یا mTLS است و نباید خودکار به محل نگهداری داده مشتری یا مسیر Bypass Policy Tenant تبدیل شود.

پیامد:

- مشتری Local/Offline می‌تواند بدون Internet، DNS عمومی، Tunnel ورودی یا سرویس مرکزی از MCP استفاده کند.

### DEC-ISMCP-003 - Context هویت استخراج‌شده از سرور و Modeها

Status: Accepted

تصمیم:

- هر فراخوانی یک `McpExecutionContext` سمت سرور با Actor، Subject، Tenant، Mode، Tool/Version، Scopeهای لازم/موثر، Risk، Approval و Correlation ID می‌سازد.
- Actor و Subject هرگز از Prompt، دستور طبیعی، metadata دلخواه یا `subjectId` تنها پذیرفته نمی‌شوند.
- Self، Actor و Subject را به مشتری جاری resolve می‌کند و Selector مربوط به `userId`/`tenantId` برای هویت دیگر را رد می‌کند.
- Admin، Actor را از Authentication معتبر انسان/سرویس/Agent می‌گیرد و Subject ساختاری را فقط بعد از Authorization مربوط به Tenant/Policy resolve می‌کند.
- Delegated از Context کوتاه‌مدت، امضاشده و audience-bound استفاده می‌کند که با expiry، audience، tenant، Tool/Scope مجاز و nonce/anti-replay محدود شده است؛ Delegation نباید مجوز را بیشتر کند.
- Security یک سطح جدا برای مدیریت Client/Scope/Permission/Secret است و Security Write در MVP پیش‌فرض غیرفعال می‌ماند.

Scopeهای لازم:

```text
identity.mcp.self.read
identity.mcp.self.write
identity.mcp.admin.read
identity.mcp.admin.write
identity.mcp.security.read
identity.mcp.security.write
```

پیامد:

- Authentication، Authorization و Tool Policy باید همگی همان مرز Mode/Scope/Tenant را enforce کنند. Prompt لایه Authorization نیست.

### DEC-ISMCP-004 - سطح Toolهای Atomic و Business

Status: Accepted

تصمیم:

- MCP هم Toolهای Atomic محدود و هم Toolهای Business هدف‌محور expose می‌کند.
- هر Tool باید `kind`، `audience`، `risk`، `approvalRequired`، `requiredScopes` و Contract Version را اعلام کند.
- Business Tool می‌تواند چند Use Case Application را orchestrate کند، اما نباید قواعد Domain را تکرار کند.
- Atomic Tool باید capability-shaped و Reviewشده باشد و نباید بدون Review به Dump Controllerها تبدیل شود.
- `call_identity_api` عمومی، اجرای دلخواه Endpoint/SQL/Repository و Tool مربوط به Token/Secret خام ممنوع است.
- خروجی Tool DTO هدفمند و redact‌شده است. Access/Refresh Token خام، Client Secret، OTP، مواد امضای JWT، credential دیتابیس و payload حساس unmasked هرگز برگردانده نمی‌شوند.

پیامد:

- MVP با Allowlist مشخص و عمدتاً Read-only شروع می‌شود؛ Write و عملیات حساس فقط با Policy و Approval مخصوص فعال می‌شوند.

### DEC-ISMCP-005 - مرز Transport، استفاده Offline و Connector Cloud

Status: Accepted

تصمیم:

- `stdio` مسیر ترجیحی Local است.
- HTTP محلی فقط fallback احراز‌شده روی `127.0.0.1`/`::1` است؛ Bind عمومی، DNS عمومی، NAT، Port Forwarding و Reverse Proxy بدون Authentication ممنوع است.
- مصرف روی سرور مشتری با HTTPS احراز‌شده یا کنترل‌های معادل شبکه خصوصی انجام می‌شود.
- AI Cloud مستقیماً به MCP آفلاین/محلی دسترسی ندارد و فقط با MCP Client/Desktop Connector مورد اعتماد محلی، Consent مشتری، Redaction و Egress Policy می‌تواند از آن استفاده کند.
- Mutation حساس باید Approval متصل به Tool/Action/Resource دقیق داشته باشد؛ تایید طبیعی مکالمه‌ای به‌تنهایی Approval نیست.
- Audit باید Actor، Subject، Tenant، Mode، Tool/Version، Scopeها، Decision، Approval، Result و Correlation ID را با Redaction ثبت کند.

پیامد:

- Contract قابلیت و امنیت در نصب Local و Server یکسان است و فقط Transport/Deployment متفاوت می‌شود.

### DEC-ISMCP-006 - مرز آینده AD/LDAP، Keycloak و Gateway سازمانی

Status: Deferred / Contract Recorded

تصمیم:

- مسیر Federation سازمانی آینده:

```text
AD/LDAP -> Keycloak User Federation -> OIDC/OAuth2 -> Identity MCP / Corevia MCP Gateway اختیاری آینده
```

- MCP نباید مستقیماً به AD/LDAP bind شود.
- Federation مربوط به Keycloak/AD و Gateway مدیریت‌شده، Integrationهای اختیاری آینده و خارج از Runtime فعلی هستند.

پیامد:

- Federation سازمانی به Decision و Phase اجرایی جدید نیاز دارد و نمی‌تواند به‌صورت پنهان وارد MVP Local/Customer Server شود.

## Run / Usage

- قبل از هر فاز وابسته، Status و Evidence Decision مربوطه بررسی شود.
- تغییر یک Decision انتخاب‌شده نیازمند Record جدید و ثبت Impact در Roadmap و Identity Specs است.

## Validation / Verification

- هر Record باید Status، گزینه یا Alternative، Decision، Consequence و اثر روی Roadmap/Spec داشته باشد.
- Decisionهای Accepted باید به فاز implementation ارجاع داشته باشند.
- Secret خام یا داده مشتری در این فایل وجود نداشته باشد.
- Decision Log انگلیسی/فارسی از نظر معنا همگام باشد.

## Troubleshooting

- اگر Tool به API/Controller/EF/Repository نیاز داشت، به `DEC-ISMCP-001` برگردید و مرز Application را بازطراحی کنید.
- اگر Deployment به Gateway اجباری یا Localhost عمومی نیاز داشت، به `DEC-ISMCP-002`/`DEC-ISMCP-005` برگردید و Decision جدید باز کنید.
- اگر هویت از Prompt یا `subjectId` خام می‌آید، Fail-Closed کنید و به `DEC-ISMCP-003` برگردید.
- اگر Provider سازمانی جدید پیشنهاد شد، تا طراحی و تایید مرز AD/LDAP -> Keycloak -> OIDC/OAuth2 آن را Deferred نگه دارید.

## Change Log

- 2026-08-13: تصمیم‌های تاییدشده sibling MCP، امنیت، Tool، Deployment و Integrationهای سازمانی آینده در IdentityService ثبت شد.

## Ownership

Product Engineering / IdentityService team
