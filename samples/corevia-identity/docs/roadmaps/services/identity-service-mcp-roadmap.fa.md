[English](./identity-service-mcp-roadmap.md) | [والد](../roadmap.fa.md) | [اسپک Identity](../../specs/README.fa.md) | [Decision Log](../../decisions/identity-service-mcp-decision-log.fa.md)

> سند تاریخی منتقل‌شده از مونورپوی corevia-market در تاریخ 2026-10-04 (مرحله 5b از Phase C). مسیرها و دستورهایی مانند `IdentityService/...` و `corevia-market/...` چیدمان مونورپو را در زمان خود توصیف می‌کنند؛ در این مخزن همان پروژه‌ها در ریشه مخزن قرار دارند و مقادیر MCP زیر `.corevia/` هستند (`.corevia/mcp/identity.values.yaml`). مقادیر اجرای فاز که در اینجا ارجاع شده‌اند در corevia-market باقی می‌مانند.

# رودمپ Adapter مربوط به MCP در IdentityService

## Endpoint / Definition of Done

- `IdentityService.Mcp` به‌صورت یک Adapter انتقالی sibling در کنار `IdentityService.Api` پیاده‌سازی شود.
- MCP مستقیماً Use Caseهای تاییدشده Application/Handlerهای MediatR را فراخوانی کند و هرگز Mirror یا Client مربوط به HTTP API، Controller، مسیر Gateway/Ocelot، EF Core، Repository یا SQL نباشد.
- دسترسی Self، Admin و Security با Context احراز‌شده سرور، Authorization و Tool Policy enforce شود؛ دسترسی Delegated مشتری یک Context اجرای امضاشده زیر Self است، Mode چهارم یا Escalation نیست.
- Scopeهای MCP، مرز Tenant، قواعد Actor/Subject، Approval، Audit، Redaction و مدل ترکیبی Atomic/Business Tool با تست مثبت و منفی پیاده‌سازی شوند.
- انتشار فعلی deployment-neutral باشد:
  - نصب روی سیستم مشتری با `stdio` به‌عنوان مسیر ترجیحی و HTTP احراز‌شده روی loopback به‌عنوان fallback؛
  - نصب روی سرور مشتری با HTTPS احراز‌شده یا HTTP خصوصی در شبکه مورد اعتماد با کنترل‌های معادل.
- Corevia MCP Gateway مرکزی dependency فعلی runtime نباشد. Gateway مدیریت‌شده آینده فقط یک relay/policy layer اختیاری، پشت connector امن outbound یا mTLS، خواهد بود.
- هوش مصنوعی Cloud برای استفاده از MCP آفلاین/محلی فقط از مسیر MCP Client یا Desktop Connector مورد اعتماد روی سیستم مشتری استفاده کند و مستقیماً به MCP محلی دسترسی نداشته باشد.
- اتصال AD/LDAP فقط برای آینده و از مسیر `AD/LDAP -> Keycloak User Federation -> OIDC/OAuth2 -> Identity MCP` طراحی شود؛ MCP هرگز مستقیماً به AD/LDAP bind نمی‌شود.
- پیش از بستن رودمپ، implementation، تست‌ها، طبقه‌بندی Runtime، اسپک‌های دوزبانه، changelog، اثر نسخه و evidence کامل باشند.

## Roadmap Links

- Parent: [نقطه ورود Roadmap مارکت](../roadmap.fa.md)
- Previous: [Roadmap ارتباطات Module-First مارکت](https://gitlab.getcorevia.ir/corevia/market/-/blob/develop/docs/roadmaps/services/market-module-first-communication-roadmap.fa.md)
- Next: TBD
- Depends On: [Corevia Standards](http://gitlab.getcorevia.ir/corevia/standards/-/blob/develop/README.fa.md)، [استاندارد ایجاد و مهاجرت سورس MCP](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/README.fa.md)، [Pattern واحد ایجاد یا مهاجرت MCP سرویس](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/patterns/mcp-service.create-or-migrate/README.fa.md)، [Values ایجاد یا مهاجرت MCP Identity](../../../.corevia/mcp/identity.values.yaml)، [Values ایجاد Source MCP Identity](../../../.corevia/operations/identity-service-mcp-source.values.yaml)، [Values مهاجرت MCP Identity](../../../.corevia/mcp/identity.values.yaml)، [Manifest مربوط به MCP Identity](../../../.corevia/mcp/identity-service.yaml)، [اسپک‌های IdentityService](../../specs/README.fa.md)، [Decision Log مربوط به MCP Identity](../../decisions/identity-service-mcp-decision-log.fa.md)
- Updates: `IdentityService`، `IdentityService.Application`، `IdentityService.Api`، `IdentityService.Mcp`، `.corevia/mcp/identity-service.yaml`، `.corevia/operations/identity-service-mcp-source.values.yaml` و اسپک‌های دوزبانه IdentityService

## Follows

```yaml
follows:
  corevia_standards: corevia-standards@0.1.0
  roadmap_governance: roadmap-governance-standard@0.1.0
  roadmap_pattern: roadmap.create-or-update@0.3.0
  phase_execution: roadmap-phase.execute@0.1.0
  service_mcp_entrypoint: mcp-service.create-or-migrate@0.1.0
```

## Purpose

این رودمپ معماری تاییدشده MCP برای Identity را به یک Adapter sibling امن، قابل تست و قابل استقرار تبدیل می‌کند. مرزهای Clean Architecture و CQRS حفظ می‌شوند و در عین حال Agent مدیریتی داخلی یا Agent مجاز مشتری می‌تواند قابلیت‌های Identity را از طریق MCP مصرف کند.

## Scope

- Inventory سورس فعلی Identity API، Use Caseهای Application، Entityهای Domain، ثبت Infrastructure، رفتار JWT/Client/Scope/Permission/Session/Tenant و Hookهای Audit موجود.
- ساخت Adapter جدید `IdentityService.Mcp` مستقل از HTTP API و مستقل از محل استقرار.
- ساخت `McpExecutionContext` شامل `Actor`، `Subject`، `Tenant`، `Mode`، Scopeها، Risk، Approval، نسخه Tool و داده Correlation.
- Modeهای Self، Admin و Security، به‌علاوه Context مشتری Delegated امضاشده و کوتاه‌مدت، با Scopeهای روشن MCP و مرز Tenant.
- قرارداد Tool ترکیبی Atomic و Business با Schema نسخه‌دار، DTOهای redact‌شده، Risk، Audience، Approval و Scopeهای لازم.
- Transport محلی `stdio`/loopback، Transport سرور مشتری HTTPS/private-network، مرز Client Cloud محلی و مرز Gateway مدیریت‌شده آینده.
- تست‌های Unit، Component، Integration، Transport، Security-negative، مستندات و evidence انتشار/راه‌اندازی.

موارد خارج از Scope انتشار فعلی:

- اجباری‌کردن Corevia MCP Gateway مرکزی.
- Reverse Proxy کردن همه درخواست‌های MCP از HTTP API فعلی یا expose کردن Mirror endpointها.
- دسترسی مستقیم MCP به EF Core، Repository، SQL، credential دیتابیس یا Secretهای Infrastructure.
- Bind مستقیم به AD/LDAP، استقرار Keycloak یا پیاده‌سازی Federation سازمانی.
- Expose کردن OTP send/verify، صدور RefreshToken، Token خام، Client Secret، مواد امضای JWT یا Toolهای Security write در MVP.
- Publish پکیج، Deploy محیط واقعی، Migration داده مشتری یا Mutation Providerهای بیرونی بدون Gate مستقل.

## Prerequisites

- `corevia-standards` در کنار `corevia-market` در workspace موجود باشد.
- نقطه ورود MCP سطح سرویس، `corevia-market/.corevia/operations/identity-service-mcp-create-or-migrate.values.yaml` است و قبل از انتخاب Pattern فرزند باید اعتبارسنجی شود.
- Contract مربوط به MCP Corevia از طریق `corevia-market/.corevia/mcp/identity-service.yaml` مصرف، با `mcp-source.create-or-update` ایجاد و با `mcp-contract.validate` اعتبارسنجی شود.
- تغییرات معماری/اسپک فعلی در `IdentityService/AGENTS.md`، `IdentityService/docs/specs/overview.md`، `contracts.md` و `acceptance.md` در دسترس باشد.
- تغییرات محلی موجود قبل از implementation inventory شوند و کار نامرتبط کاربر حفظ شود.
- هر تغییر Runtime/Config/Deployment از نظر اثر روی نسخه پروژه و نسخه محیط واقعی بررسی شود.
- هیچ فازی قبل از تولید plan مربوط به `roadmap-phase.execute` و تایید انسانی همان فاز اجرا نشود.

## Configuration

- Values اصلی رودمپ: `corevia-market/.corevia/operations/identity-service-mcp-roadmap.values.yaml`.
- Values اجرای فاز: `corevia-market/.corevia/operations/identity-service-mcp-roadmap-phase.values.yaml`.
- Values canonical مسیریابی سطح سرویس MCP: `corevia-market/.corevia/operations/identity-service-mcp-create-or-migrate.values.yaml`.
- تنظیمات غیرحساس MCP باید از مسیر Runtime استاندارد Corevia، یعنی Consul/`ConfigCenterExtension` و `ConfigLoaderExtension`، تامین شوند.
- Credentialهای MCP، کلیدهای امضا، Client Secret، Secretهای Approval و هر مقدار حساس دیگر باید در Vault/`SecretStoreExtension` باشند و نباید در `appsettings.json`، فایل رودمپ، Schema Tool یا مستندات `.env` hard-code شوند.
- `AddConfigLoaderExtension(...)` باید قبل از Options Binding و قبل از هر Registration مصرف‌کننده کانفیگ MCP اجرا شود.
- `stdio` محلی نباید برای کارکردن به Consul، Vault، Gateway مرکزی یا Internet نیاز داشته باشد؛ اما هر کانفیگ اختیاری همچنان باید استاندارد Runtime سرویس را رعایت کند.
- قواعد عمومی MCP مالکیت `corevia-standards` هستند؛ Abstractionهای Runtime مشترکِ اثبات‌شده در صورت نیاز به Package تأییدشده `corevia-kit` می‌روند و Bridge فقط Executor رسمی Runtime/Deployment را مالک می‌شود.

## Pattern Detection Gate

Execution Pattern: `roadmap-phase.execute`

Execution Values: `corevia-market/.corevia/operations/identity-service-mcp-roadmap-phase.values.yaml`

قبل از هر فاز، Agent باید فرمان plan استاندارد Corevia را اجرا کند، اولین فاز باز، ریسک‌ها، فایل‌ها، Verification و Gateهای جدا را گزارش دهد و برای تایید صریح منتظر بماند. تایید فاز به معنی تایید Commit/Push، Publish پکیج، Deploy، Mutation Gateway، Mutation Consul/Vault یا عملیات Destructive نیست.

## Gateهای باقی‌مانده برای بستن رودمپ

Baseline پیاده‌سازی محلی کامل است، اما رودمپ تا ثبت Gateهای تحویل زیر بسته نمی‌شود:

- [ ] Branchهای Standards، Bridge و Market بدون واردکردن تغییرات نامرتبط ایجنت‌های دیگر Commit و Push شوند.
- [ ] Merge Requestها به‌ترتیب وابستگی باز و Merge شوند: Standards به `main`، Bridge به `main` و Market/Identity به `develop`.
- [ ] CI ریموت هر سه Repository سبز شود؛ از جمله Restore احراز‌شده Packageهای خصوصی در جاهایی که Pipeline به Nexus نیاز دارد.
- [ ] Evidence ریموت/Release فاز ۷ تأییدشده و یک Smoke واقعی MCP Identity روی Transport محلی یا سرور مشتری اجرا شود؛ Validate محلی Contract به‌تنهایی کافی نیست.
- [ ] فاز ۸ به‌صورت جدا برای Pilot مشتری، Publish و Deploy تولید باز شود. هرکدام Approval، Health/Readback، Evidence ماسک‌شده و Rollback مستقل می‌خواهند.
- [ ] Executor قابل‌استفاده برای سرویس‌های آینده به‌عنوان Follow-up صریح باقی بماند: تا زمان Approval و پیاده‌سازی Executor فرزند `apply`، Pattern `mcp-service.create-or-migrate` فقط Facade مربوط به Validate/Plan است و برای Mutation باید `extension-needed` بدهد.
- [ ] Gateway مرکزی، Federation مربوط به AD/LDAP و Keycloak و Integrationهای آینده اختیاری بمانند و برای بستن رودمپ فعلی استفاده نشوند.

## Minimum Prompt Contract

برای Context جدید:

```text
طبق Corevia Standard و این Roadmap ادامه بده. فقط اولین فاز باز را plan کن و قبل از implementation منتظر تایید صریح بمان:
corevia-market/docs/roadmaps/services/identity-service-mcp-roadmap.fa.md
```

برای implementation بعد از تایید یک فاز مشخص:

```text
طبق Corevia Standard و contract فاز تاییدشده برای این Roadmap ادامه بده:
corevia-market/docs/roadmaps/services/identity-service-mcp-roadmap.fa.md
فقط همان فاز را اجرا کن. Publish، Deploy، Commit/Push، Mutation خارجی و عملیات Destructive همچنان Approval جدا دارند.
```

## Run / Usage

- اعتبارسنجی Values رودمپ:
  `corevia-standards/bin/corevia-run validate --pattern roadmap.create-or-update --values corevia-market/.corevia/operations/identity-service-mcp-roadmap.values.yaml`
- Plan اولین فاز باز:
  `corevia-standards/bin/corevia-run plan --pattern roadmap-phase.execute --values corevia-market/.corevia/operations/identity-service-mcp-roadmap-phase.values.yaml`
- `corevia-run apply` فقط بعد از تایید همان فاز اجرا شود.
- Evidence فازها در `corevia-bridge/.work/roadmap-phase-executions/identity-service-mcp-roadmap/` ثبت و فقط بر اساس evidence در هر دو زبان Roadmap به‌روزرسانی شود.

## Architecture Decision Brief

- مسئله: قابلیت‌های Identity باید بدون ساختن کپی ناامن HTTP API، بدون نشت Secretهای Identity و بدون مجبورکردن همه مشتری‌ها به سرویس مرکزی در اختیار Agent قرار بگیرند.
- گزینه A: افزودن `IdentityService.Mcp` به‌عنوان sibling Adapter که مستقیماً Use Caseهای Application را فراخوانی می‌کند. این گزینه Clean Architecture را حفظ می‌کند، Local/Offline و Customer Server را پشتیبانی می‌کند و مرز Policy مستقل برای MCP می‌سازد.
- گزینه B: پیاده‌سازی MCP به‌صورت HTTP Client یا Endpoint Mirror روی `IdentityService.Api`. این گزینه سریع‌تر routeها را reuse می‌کند، اما MCP را به Transport و جزئیات API وابسته می‌کند، Tool Contract را مبهم می‌سازد و سطح expose گسترده ایجاد می‌کند.
- گزینه C: اجباری‌کردن Corevia MCP Gateway برای همه نصب‌ها. این گزینه Routing مدیریت‌شده را ساده می‌کند، اما مشتری Offline/Local را مسدود و dependency غیرضروری runtime ایجاد می‌کند.
- پیشنهاد: اکنون گزینه A؛ گزینه C فقط به‌عنوان Mode مدیریت‌شده اختیاری آینده حفظ شود.
- انتخاب: گزینه A انتخاب شد. گزینه B رد شد. گزینه C Deferred است و نباید جلوی Local یا Customer Server را بگیرد.

## Decision Records

جزئیات در [Decision Log مربوط به MCP Identity](../../decisions/identity-service-mcp-decision-log.fa.md) نگهداری می‌شود:

- `DEC-ISMCP-001`: مرز sibling و فراخوانی مستقیم Application/MediatR.
- `DEC-ISMCP-002`: Modeهای فعلی Local/Customer Server و Deferred بودن Gateway مرکزی.
- `DEC-ISMCP-003`: Actor/Subject استخراج‌شده توسط سرور، Modeهای Self/Admin/Security، Context مشتری Delegated امضاشده و مرز Tenant.
- `DEC-ISMCP-004`: Toolهای ترکیبی Atomic/Business و metadata اجباری Tool.
- `DEC-ISMCP-005`: Transport محلی، Connector برای Cloud، مرز Offline و Redaction.
- `DEC-ISMCP-006`: مرز آینده AD/LDAP، Keycloak Federation، OIDC/OAuth2 و Gateway مدیریت‌شده اختیاری.

## Non-Negotiable MCP Invariants

ناوردای عمومی این بخش از [استاندارد ایجاد و مهاجرت سورس MCP Corevia](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/contracts.fa.md)، [Baseline پذیرش A01 تا A16](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/acceptance.fa.md) و [قرارداد Migration](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/migration.fa.md) به ارث می‌رسند. Manifest اختصاصی Identity در [`.corevia/mcp/identity-service.yaml`](../../../.corevia/mcp/identity-service.yaml) است. این Roadmap قواعد را برای Identity map و test می‌کند و نباید آن‌ها را ضعیف یا دوباره تعریف کند.

### Execution context and identity

هر فراخوانی باید یک `McpExecutionContext` سمت سرور بسازد که شامل این موارد باشد:

- `Actor`: انسان، Service Client یا Agent Client احراز‌شده‌ای که درخواست را آغاز کرده است.
- `Subject`: User، Tenant یا Session مورد عملیات.
- `Tenant`: مرز Tenant resolve‌شده.
- `Mode`: یکی از `self`، `admin` یا `security`؛ دسترسی Delegated مشتری Context امضاشده زیر `self` است.
- نام/نسخه Tool، Scopeهای لازم/موثر، Risk، وضعیت Approval و `CorrelationId`.

`Actor` و `Subject` هرگز نباید از Prompt، دستور طبیعی، metadata دلخواه Caller، یا `subjectId` تنها پذیرفته شوند. سرور آن‌ها را از Authentication و Policy معتبر استخراج می‌کند.

### Authorization modes and scopes

| Mode | قاعده Actor/Subject | مرز Scope | قاعده MVP |
|---|---|---|---|
| `self` | Actor و Subject همان مشتری احراز‌شده فعلی هستند | `identity.mcp.self.read` / `identity.mcp.self.write` | `userId`، `tenantId` یا Selector کاربر دیگر پذیرفته نمی‌شود |
| `admin` | Actor از Authentication معتبر انسان/سرویس/Agent می‌آید؛ Subject resolve و authorize می‌شود | `identity.mcp.admin.read` / `identity.mcp.admin.write` | همه عملیات tenant-scoped هستند؛ Tenant مبهم/غایب رد می‌شود |
| `delegation زیر self` | Subject از Context امضاشده، کوتاه‌مدت و audience-bound می‌آید | Self capability محدود؛ Delegation نباید Scope را بیشتر کند | expiry، audience، tenant، Tool/Scope مجاز و nonce/anti-replay بررسی می‌شود |
| `security` | فقط Context مدیریتی امنیتی | `identity.mcp.security.read` / `identity.mcp.security.write` | مدیریت Client/Scope/Permission/Secret در MVP پیش‌فرض غیرفعال است |

شش خانواده Scope مربوط به MCP از Scopeهای API جدا هستند. علاوه بر آن، هر مجوز Authorization API یک Mapping و Scope گرانولار MCP متناظر دارد (۳۶ Mapping فعال Authorization و پنج Binding صریح غیرقابل‌Expose برای Inventory عملیات پروتکلی):

```text
identity.mcp.self.read
identity.mcp.self.write
identity.mcp.admin.read
identity.mcp.admin.write
identity.mcp.security.read
identity.mcp.security.write
```

Authentication، Authorization و Tool Policy باید همگی این قواعد را enforce کنند. Prompt، توضیح Tool، metadata کاربر یا تایید طبیعی هرگز مجوز ایجاد نمی‌کند.

### Delegation, approval, audit, and redaction

- Context مربوط به Delegation باید کوتاه‌مدت، امضاشده، audience-bound، tenant-bound و محدود به Tool/Scopeهای نام‌برده باشد؛ Context منقضی، بدون امضا، با Audience اشتباه، گسترده، یا replay‌شده رد می‌شود.
- Write حساس باید Approval متصل به Tool، Action و Resource دقیق داشته باشد. «بله» گفتن در مکالمه Approval معتبر نیست.
- Audit باید Actor، Subject، Tenant، Mode، نام/نسخه Tool، Scopeهای لازم/موثر، تصمیم Authorization، id/status Approval، وضعیت نتیجه و correlation id را ثبت کند.
- خروجی Tool و Audit باید redact شوند. Access/Refresh Token خام، Client Secret، OTP، مواد امضای JWT، credential دیتابیس و payload حساس بدون masking هرگز برگردانده یا ثبت نمی‌شوند.

### Tool model

MCP هر دو نوع زیر را expose می‌کند:

- **Atomic:** یک قابلیت محدود و نزدیک به Use Case Application؛ مثل `identity_get_self_profile`، `identity_list_self_sessions` یا `identity_get_user`.
- **Business:** یک هدف سطح‌بالای کاربر/Agent که می‌تواند چند Use Case Application را هماهنگ کند؛ مثل `identity_get_user_access_summary` یا `identity_revoke_user_sessions`.

هر Tool حداقل این metadata را اعلام می‌کند:

```text
kind: atomic | business
audience: self | admin
risk: read | write | sensitive
approvalRequired: true | false
requiredScopes: [...]
version: <contract-version>
```

Business Tool می‌تواند چند Use Case Application را orchestrate کند، اما نباید قواعد Domain را دوباره پیاده کند. Atomic Tool نیز نباید بدون Review به dump یک‌به‌یک Controllerها تبدیل شود. `call_identity_api` عمومی، اجرای دلخواه endpoint/SQL/Repository و Tool مربوط به Token/Secret خام ممنوع است.

### Current deployment boundary

```text
مشتری Local:
  Trusted MCP Client --stdio (ترجیحی)--> IdentityService.Mcp
                                      └--> IdentityService.Application -> Infrastructure

سرور مشتری:
  Authenticated MCP Client --HTTPS/private HTTP--> IdentityService.Mcp
                                                   └--> Application -> Infrastructure

Mode مدیریت‌شده آینده (فعلاً اجرا نمی‌شود):
  Agent -> Corevia MCP Gateway اختیاری -> Secure Connector/mTLS outbound -> Customer MCP
```

در fallback HTTP محلی، سرور فقط روی `127.0.0.1`/`::1` bind می‌شود، Authentication و Authorization را حفظ می‌کند، در صورت وجود Headerهای Host/Origin را validate می‌کند، Sessionهای per-process/per-client و Credentialهای کوتاه‌مدت دارد و هرگز از طریق DNS عمومی، NAT، Port Forwarding یا Reverse Proxy بدون Authentication expose نمی‌شود. AI ابری فقط از مسیر MCP Client/Desktop Connector مورد اعتماد محلی به MCP می‌رسد و Consent مشتری، Policy خروج داده و Redaction لازم است.

### Future enterprise federation boundary

```text
Active Directory (AD/LDAP)
        -> Keycloak User Federation
        -> OIDC/OAuth2
        -> Identity MCP / Corevia MCP Gateway اختیاری آینده
```

MCP نباید مستقیماً به AD/LDAP bind شود. Keycloak/AD و Corevia MCP Gateway مرکزی integrationهای اختیاری آینده هستند و پیش‌نیاز runtime برای نصب Local/Customer Server فعلی نیستند.

## Phases

### Phase 1 - Source Inventory and Boundary Baseline

Status: Completed

Outcome:
- Inventory قابل Review ثابت می‌کند سورس فعلی Identity چگونه می‌تواند Adapter sibling MCP را بدون عبور از مرز Clean Architecture میزبانی کند.

Deliverables:
- Map پروژه/Reference برای Api، Application، Domain، Infrastructure و پروژه برنامه‌ریزی‌شده Mcp.
- Catalog قابلیت‌های Use Case برای Users، Profile، Sessions، UserRoles، UserTenants، Tenants، Roles، Permissions، Scopes، Clients و Joinهای Client/Scope/Permission.
- Baseline احراز هویت، JWT، M2M Client، Tenant، CORS، Discovery، Runtime Provider و Audit.
- Matrix اولیه Toolهای کاندید با Audience/Risk، Atomic/Business و Handler احتمالی Application.
- Gap list روشن برای Approval، Delegation، Context، Redaction و Transport MCP.

Tasks:
- [x] Referenceهای پروژه، `Program.cs`، Controllerها، Handlerهای Application، Interfaceهای Domain، Registrationهای Infrastructure و تست‌های موجود بررسی شوند.
- [x] Command/Queryهای Application به قابلیت‌های Self/Admin نگاشت شوند، بدون طراحی Endpoint Mirror.
- [x] Claimهای JWT، رفتار Client/Scope/Permission/Role/Tenant/Session و ورودی‌های server-owned مربوط به Actor/Subject inventory شوند.
- [x] CORS و Transport فعلی API audit شوند تا Policy باز API به MCP به ارث نرسد.
- [x] هر مقدار Runtime پیشنهادی MCP به Consul Config، Vault Secret، Environment/Bootstrap یا Local-only Default طبقه‌بندی شود.
- [x] Inventory و Gapهای باز در evidence فاز ثبت شوند.

Acceptance:
- [x] هر قابلیت کاندید Owner، Entry Point در Application، Audience، Risk، Sensitivity و Exclusion یا فاز بعدی مشخص دارد.
- [x] Implementation MCP از فهرست Routeهای Controller شروع نشده است.
- [x] تغییرات محلی نامرتبط و وضعیت Runtime/Version بدون Mutation ثبت شده‌اند.

Verification:
- `rg --files IdentityService`
- `rg -n "AddAuthentication|AddAuthorization|JwtBearer|AddMediatR|AllowAnyOrigin|Controller|Client|Scope|Permission|Tenant|Session" IdentityService --glob '*.cs' --glob '*.csproj' --glob 'appsettings*.json'`
- `dotnet sln IdentityService/IdentityService.sln list` یا فرمان canonical مخزن
- Review سورس/Reference و Scan بدون Secret

Evidence:
- گزارش فاز اول زیر `corevia-bridge/.work/roadmap-phase-executions/identity-service-mcp-roadmap/`.
- Referenceهای Inventory در Decision/Specهای MCP مربوط به IdentityService.

### Phase 2 - Architecture and Contract Freeze

Status: Completed

Outcome:
- مرز sibling، Modeهای اجرا، Scopeها، مدل Tool، مرز Transport و Integrationهای آینده به‌عنوان Contract پیاده‌سازی پذیرفته شوند.

Deliverables:
- Decisionهای پذیرفته‌شده `DEC-ISMCP-001` تا `DEC-ISMCP-006`.
- اسپک‌های دوزبانه Overview، Contracts، Acceptance و Changelog همگام.
- Baseline نسخه‌دار برای `McpExecutionContext`، metadata Tool، نتیجه Authorization، Approval، Audit و Redaction.

Tasks:
- [x] Sibling Adapter به‌عنوان گزینه انتخابی و HTTP Endpoint Mirror به‌عنوان گزینه ردشده ثبت شود.
- [x] استقرار فعلی Local/Customer Server تایید و dependency Gateway مرکزی Deferred شود.
- [x] Modeهای Self/Admin/Security، Context Delegation امضاشده، Scopeهای دقیق، Policy Tenant، استخراج Actor/Subject و ممنوعیت Authorization با Prompt تایید شوند.
- [x] مدل Atomic/Business، metadata اجباری، DTO redact‌شده و Exclusionهای MVP تایید شوند.
- [x] Safeguardهای Transport محلی/Cloud و مرز آینده AD/LDAP -> Keycloak -> OIDC/OAuth2 ثبت شوند.
- [x] دلیل، پیامد و evidence در Decision Log دوزبانه ثبت شود.

Acceptance:
- [x] هیچ تصمیم معماری در Prompt یا فرض implementation پنهان باقی نماند.
- [x] Decision Log گزینه‌های انتخاب‌شده/ردشده/Deferred و پیامدشان را صریح ثبت کند.
- [x] اسپک‌های انگلیسی/فارسی از نظر معنا همگام و به این Roadmap لینک باشند.

Verification:
- `corevia-standards/bin/corevia-run validate --pattern roadmap.create-or-update --values corevia-market/.corevia/operations/identity-service-mcp-roadmap.values.yaml`
- Review دوزبانه diff برای Decision Log و Identity Specs
- Review لینک‌ها و نبود Secret خام

Evidence:
- `corevia-market/docs/decisions/identity-service-mcp-decision-log.md` و `.fa.md`.
- Changelog اسپک IdentityService و گزارش اجرای فاز.

### Phase 3 - MCP Project Boundary and Composition Root

Status: Completed

Outcome:
- `IdentityService.Mcp` به‌عنوان Adapter sibling مستقل و دارای Dependency Graph پاک ساخته و Build شود.

Deliverables:
- پروژه `IdentityService/IdentityService.Mcp` با TFM مخزن و ثبت‌شده در Solution/Build canonical.
- DI/Composition مربوط به MCP که بتواند Registrationهای Infrastructure را در Composition Boundary reuse کند، بدون Reference به `IdentityService.Api`.
- Portهای Context/Policy سمت Application و مرز Mapping پروتکل به Command/Query.
- عدم وابستگی برگشتی Domain/Application به Api یا Mcp.

Tasks:
- [x] پروژه Mcp ایجاد و SDK/Transport مورد تایید پس از Review منبع، License و Package انتخاب شود.
- [x] فقط Referenceهای لازم پروژه/Package اضافه شوند و Reference به `IdentityService.Api` صریحاً رد شود.
- [x] Composition Boundary، abstraction مربوط به Context Application، Error Mapping، Cancellation و Propagation مربوط به Correlation تعریف شود.
- [x] سرویس‌های MCP از Composition Root ثبت شوند و ترتیب `AddConfigLoaderExtension(...)` و Startup فعلی حفظ شود.
- [x] Feature Switch برای MCP با Default غیرفعال و Local Default امن اضافه شود؛ Credential در Config سورس قرار نگیرد.
- [x] Checkهای Dependency/Build اضافه شوند تا Reference به Api/Controller/EF/Repository در Adapter fail شود.

Acceptance:
- [x] `IdentityService.Mcp` مستقل Build می‌شود و Reference به `IdentityService.Api` ندارد.
- [x] Domain و Application از Transport مستقل باقی می‌مانند.
- [x] Adapter فقط Contract/Handlerهای Application و Policyهای مشترک را فراخوانی می‌کند و مسیر مستقیم Controller/HTTP/EF/Repository/SQL ندارد.
- [x] وقتی MCP غیرفعال است، Startup و رفتار API فعلی تغییری نمی‌کند.

Verification:
- `dotnet list IdentityService/IdentityService.Mcp/IdentityService.Mcp.csproj reference`
- `dotnet build IdentityService/IdentityService.Mcp/IdentityService.Mcp.csproj --no-restore`
- `rg -n "IdentityService\.Api|Controller|DbContext|EntityFramework|Repository|ExecuteSql|HttpClient" IdentityService/IdentityService.Mcp IdentityService/IdentityService.Application IdentityService/IdentityService.Domain`
- تست‌های هدفمند و Build/Test فعلی Identity API

Evidence:
- Graph پروژه/Reference، خروجی Build و گزارش تست Boundary در Artifact اجرای فاز.

### Phase 4 - Authentication, Authorization, Execution Context, and Security

Status: Completed

Outcome:
- هر فراخوانی MCP قبل از اجرای Use Case Application احراز، authorize، tenant-scoped و audit شود.

Deliverables:
- `McpExecutionContext` سمت سرور و Resolver مربوط به Context معتبر هویت.
- Adapterهای Authentication برای Context مشتری، Admin انسانی، Service Client و Agent Client معتبر.
- Enforcement مربوط به هر شش خانواده Scope MCP و سه Mode (`self`، `admin`، `security`)؛ Delegation مشتری یک Context اجرای محدود زیر `self` است و Mode چهارم نیست.
- Validation مربوط به Delegation، Contract Approval، Audit، Redaction و خطاهای fail-closed.

Tasks:
- [x] نگاشت Claimهای JWT/M2M فعلی به Actor، Subject، Tenant، Mode و Scopeهای موثر MCP تعریف شود.
- [x] Self Mode بدون Selector کاربر/Tenant و با Deny صریح cross-user/cross-tenant پیاده شود.
- [x] Admin Mode با Subject resolve‌شده توسط سرور، Policy tenant-scoped و عدم حدس Tenant پیاده شود.
- [x] Context Delegation با بررسی Signature، Expiry، Audience، Tenant، Tool/Scope مجاز، Nonce/Anti-Replay و Non-Escalation پیاده شود.
- [x] Security Mode به‌عنوان Policy جدا تعریف و Security Write در MVP پیش‌فرض غیرفعال بماند.
- [x] Mutation حساس فقط Approval متصل به Tool/Action/Resource دقیق داشته باشد؛ متن مدل یا تایید مکالمه‌ای Approval نباشد.
- [x] Audit redact‌شده با فیلدهای هویت، Policy، Approval، نتیجه، نسخه Tool و Correlation تولید شود.

Acceptance:
- [x] Actor و Subject از Prompt، metadata دلخواه یا `subjectId` تنها قابل تامین یا Override نباشند.
- [x] درخواست Expired، Unsigned، Wrong-Audience، Over-Broad، Cross-Tenant، Replayed یا بدون Approval fail-closed شود.
- [x] Access/Refresh Token خام، ClientSecret، OTP، مواد امضا، credential دیتابیس و payload حساس unredacted در خروجی یا Audit دیده نشود.
- [x] Authentication، Authorization و Tool Policy هرکدام Mode/Scope/Tenant را enforce کنند.

Verification:
- تست Unit/Component برای هر Mode، Scope، Tenant Boundary، Claim مربوط به Delegation، Approval و Redaction
- تست منفی Prompt Injection، metadata دلخواه، Selector cross-user، Tenant گمشده، Delegation نامعتبر، Replay و Approval ناقص
- Review Snapshotهای Audit با فیلدهای حساس redact‌شده

Evidence:
- گزارش تست امنیتی، Policy Matrix و نمونه Audit redact‌شده در Artifact فاز و Acceptance Spec Identity.

### Phase 5 - Tool Contract and Application Capability Mapping

Status: Completed

Outcome:
- Identity MCP یک سطح Tool محدود، نسخه‌دار و business-oriented را روی قابلیت‌های موجود Application expose کند.

Deliverables:
- Registry/Schema نسخه‌دار با `kind`، `audience`، `risk`، `approvalRequired`، `requiredScopes` و Contract Version.
- Catalog Toolهای Atomic/Business با نگاشت به Command/Queryهای Application و DTOهای redact‌شده.
- Allowlist مربوط به MVP و Disabled List برای Security Write، OTP/Token Issuance، Secret خام و Toolهای عمومی.
- Contract مربوط به Validation، Error، Pagination و Correlation برای فراخوانی Tool.

Tasks:
- [x] Toolهای Read مربوط به Self برای Profile/Access/Session و Writeهای مجاز مانند Update Profile/Revoke Session فقط در صورت Policy تعریف شوند.
- [x] Toolهای Read مربوط به Admin برای Search User، Access، Tenant Membership و Session با Subject tenant-scoped تعریف شوند.
- [x] Toolهای Write آینده Admin مانند Role/Session Changes با Risk مناسب تعریف و در موارد لازم به Approval دقیق متصل شوند.
- [x] Business Toolهای orchestrating چند Use Case Application تعریف شوند، بدون تکرار قواعد Domain؛ Atomic Toolها محدود و Reviewشده باشند.
- [x] `call_identity_api`، Endpoint/SQL/Repository دلخواه، Tool Token/Secret خام، OTP Login/Verify/Refresh و Security Write از Allowlist پیش‌فرض MVP حذف شوند.
- [x] Schema Validation، Error Mapping پایدار، DTO redact‌شده، Versioning و Compatibility/Deprecation Policy اضافه شود.
- [x] Contract Tool قبل از فعال‌سازی Runtime در اسپک دوزبانه Identity منتشر شود.

Acceptance:
- [x] هر Tool expose‌شده Audience، Risk، Approval Flag، Scope لازم، Version، Mapping Application و رفتار Redaction صریح دارد.
- [x] Catalog شامل Atomic و Business Tool است و به Dump Controllerها تبدیل نشده است.
- [x] Self امکان انتخاب User/Tenant دلخواه و Admin/Delegated امکان bypass Policy Tenant را ندارد.
- [x] قابلیت‌های Security و Sensitive تا فعال‌شدن Policy و Approval خودشان غیرفعال هستند.

Verification:
- تست Contract برای Schema/Registry Tool
- تست Integration Handlerهای Application که نشان دهد MCP Controllerهای API را دور می‌زند
- تست مثبت Read-only برای Self/Admin و تست منفی Generic Tool/Secret خام/Cross-Tenant
- Review Diff Contract در برابر `IdentityService/docs/specs/contracts.md` و `.fa.md`

Evidence:
- Registry Tool، Matrix نگاشت، خروجی تست Schema و Allowlist MVP در Artifact اجرای فاز.

### Phase 6 - Deployment-Neutral Transport and Runtime Configuration

Status: Completed

Outcome:
- همان Contract قابلیت MCP روی سیستم مشتری یا سرور مشتری، بدون Gateway مرکزی، به‌صورت امن اجرا شود.

Deliverables:
- Transport محلی `stdio` به‌عنوان مسیر ترجیحی.
- HTTP loopback احراز‌شده فقط روی `127.0.0.1`/`::1`.
- HTTPS احراز‌شده و HTTP شبکه خصوصی مورد اعتماد برای سرور مشتری.
- طبقه‌بندی Runtime Corevia برای Config غیرحساس، Secret، Bootstrap، Discovery و Local Default.
- مرز Client/ Desktop Connector برای Cloud و Contract Connector مربوط به Gateway آینده، بدون پیاده‌سازی Gateway فعلی.

Tasks:
- [x] Startup، Isolation Session/Process، Cancellation و Offline Operation مربوط به `stdio` پیاده و تست شود.
- [x] Fallback loopback با Authentication/Authorization فعال، Host/Origin Validation، Credential کوتاه‌مدت و بدون Public Bind/Forwarding پیاده شود.
- [x] HTTPS/Private Network سرور مشتری، Certificate/Trust، Health/Version و مرز Tenant Routing تعریف شود.
- [x] تنظیمات MCP از مسیر Consul/Vault/ConfigLoader طبقه‌بندی شود؛ Secret در appsettings، `.env`، Tool Schema و Image قرار نگیرد.
- [x] مستند شود که AI Cloud فقط با Local MCP Client/Desktop Connector مورد اعتماد، Consent مشتری و Egress Policy به MCP محلی می‌رسد.
- [x] Mode مدیریت‌شده آینده به‌شکل `Agent -> Corevia MCP Gateway اختیاری -> Secure Connector/mTLS outbound -> Customer MCP` ثبت شود؛ فعلاً پیاده نشود.

Acceptance:
- [x] `stdio` محلی بدون Gateway مرکزی، DNS عمومی، Tunnel ورودی یا Internet کار می‌کند.
- [x] HTTP loopback نمی‌تواند Public Bind شود یا بدون Auth/Authz کار کند.
- [x] Mode سرور مشتری HTTPS احراز‌شده یا کنترل‌های معادل شبکه خصوصی را لازم دارد.
- [x] مشتری می‌تواند Local یا Server را بدون تغییر Contract Tool/Security انتخاب کند.
- [x] هیچ Dependency فعلی به Gateway مرکزی، AD/LDAP یا Keycloak به Runtime اضافه نشده است.

Verification:
- Smoke Test محلی Offline برای `stdio`
- تست Bind عمومی و Forwarding بدون Authentication
- تست Transport HTTPS/Private Network سرور مشتری
- Runtime-provider validator و Review طبقه‌بندی Config
- Review لینک‌های مستندات و Runbook استقرار

Evidence:
- Matrix Transport، گزارش Config/Provider، Smoke Reportهای Local/Server و evidence Policy Connector redact‌شده.

### Phase 7 - Test, Documentation, and Release Readiness

Status: In Progress

Outcome:
- MCP با evidence عینی Source، Security، Contract، Build، Documentation و Compatibility برای Release آماده باشد.

Deliverables:
- Suite تست Unit، Integration، Component، Transport، Security-negative و Contract.
- README، AGENTS، overview/contracts/acceptance/changelog، Roadmap و Decision Log دوزبانه Identity به‌روزشده.
- Record اثر نسخه؛ در صورت تغییر Runtime، نسخه پروژه و Version Key محیط واقعی همگام شود.
- Checklist انتشار که Publish، Deploy، Rollout مشتری و Mutationهای بیرونی را Gate جدا نگه دارد.

Tasks:
- [x] تست‌های هدفمند MCP/Application/Domain و تست‌های موجود Identity API اجرا شوند.
- [x] Matrix کامل Security-negative شامل Prompt Actor/Subject Injection، Cross-user/Tenant، Delegation نامعتبر، Replay، Approval ناقص، Secret خام، Public Bind و نبود Gateway اجرا شود.
- [x] Build، Dependency Boundary، Schema Compatibility و Smoke محلی Loopback/stdio اجرا شوند.
- [x] Smoke مربوط به HTTPS/Private Network سرور مشتری با Certificate و مرز Trust Provisionشده اجرا شود؛ این مورد از Evidence محلی Loopback جداست.
- [x] اسپک‌ها و Changelog دوزبانه بر اساس Implementation واقعی و Evidence به‌روز شوند، نه ادعای Planned.
- [x] Validation رودمپ Corevia، Plan/Status/Evidence فاز، Validator مخزن، Scan بدون Secret و Documentation Audit اجرا شود.
- [x] اثر Version از تغییر واقعی Runtime/Config/Deployment تعیین شود و فقط در صورت نیاز `.csproj`، Version محیط واقعی و Docs همگام شوند.

Acceptance:
- [ ] تست‌های مثبت و منفی اجباری پاس شده یا Blocker دارای Owner مشخص باشد؛ Security Blocker بی‌صدا Waive نشود.
- [ ] Docs، Linkها، Specها، Decisionها و Evidence دوزبانه همگام باشند.
- [ ] Publish پکیج، Deploy، Mutation Gateway، Integration AD/Keycloak یا Rollout مشتری بدون Evidence و Approval مستقل Complete گزارش نشود.

Verification:
- `dotnet test` برای Solution/Projectهای متاثر
- `corevia-standards/bin/corevia-run validate --pattern roadmap.create-or-update --values corevia-market/.corevia/operations/identity-service-mcp-roadmap.values.yaml`
- `corevia-standards/bin/corevia-run plan --pattern roadmap-phase.execute --values corevia-market/.corevia/operations/identity-service-mcp-roadmap-phase.values.yaml`
- `ruby ../corevia-standards/validators/corevia-standards/validate.rb --target corevia-market --check-repo`
- `bash scripts/docs/audit.sh`

Evidence:
- گزارش نهایی تست/Release در Artifact فاز و Specهای لینک‌شده Identity.

### Phase 8 - Controlled Customer Pilot and Future Managed-Mode Readiness

Status: Planned

Outcome:
- Pilot کنترل‌شده مشتری، مصرف MCP در Mode Local یا Customer Server را ثابت کند و هم‌زمان مرز فعلی بدون Gateway و نقطه اتصال مدیریت‌شده آینده را حفظ کند.

Deliverables:
- Runbook نصب/استفاده برای Local `stdio` و Customer Server HTTPS/Private Network.
- Checklist Consent مشتری، Egress، Redaction، Rotation Credential، Disable/Rollback و Review Audit.
- Evidence Health/Version/Capability و بازخورد Compatibility در Pilot.
- یادداشت Interface مربوط به Gateway/Connector آینده بدون Dependency فعلی.

Tasks:
- [ ] Mode Pilot و Allowlist دقیق Tool/Scope با Approval ثبت شود.
- [ ] MCP Server با MCP Client محلی مورد اعتماد یا Client سرور مشتری نصب و اجرا شود؛ Localhost عمومی باز نشود.
- [ ] Offline Local، مرز Connector Cloud در صورت نیاز، Tenant Isolation، Audit Redaction، Disable/Rollback و Expiry/Rotation Credential بررسی شود.
- [ ] محدودیت‌های مشتری و Toolهای درخواستی جدید به‌عنوان کار Decision-gated ثبت شوند.
- [ ] Roadmap فقط بعد از ثبت Evidence Pilot و همه Approvalهای جداگانه Publish/Deploy/Customer Rollout بسته شود.

Acceptance:
- [ ] مشتری بتواند Mode تاییدشده Local یا Server را بدون Gateway مرکزی مصرف کند.
- [ ] Credential، Token، OTP، Secret خام، داده Cross-Tenant یا Mutation بدون Approval قابل مشاهده نباشد.
- [ ] Gateway/AD/Keycloak آینده همچنان اختیاری و خارج از Contract فعلی Deployment بماند.

Verification:
- اجرای Runbook Pilot و Health/Version Check
- Audit Allowlist Tool/Scope و Review Egress مشتری
- تست Rollback/Disable و Expiry Credential

Evidence:
- گزارش Pilot، Approval مشتری و Evidence نهایی رودمپ زیر Work Root مربوط به Corevia Bridge.

## شواهد اجرای فعلی

- سورس Runtime: پروژه `IdentityService/IdentityService.Mcp` به‌عنوان Adapter sibling پیاده‌سازی شده و به پروژه API Reference ندارد.
- Build محلی: فرمان `dotnet build IdentityService/IdentityService.Mcp/IdentityService.Mcp.csproj --no-restore` با موفقیت اجرا شد.
- تست‌های امنیتی و Policy: فرمان `dotnet test IdentityService/IdentityService.Mcp.Tests/IdentityService.Mcp.Tests.csproj --no-restore` با موفقیت Suite کامل Catalog و Security و صفر خطا اجرا شد.
- Smoke HTTP: روی Loopback و پورت `۵۲۹۱`، پاسخ `/health` برابر `۲۰۰`، درخواست ناشناس `/mcp` برابر `۴۰۱`، Host نامعتبر برابر `۴۰۰` بود و Process بدون خطا متوقف شد.
- Smoke پروفایل سرور مشتری: HTTPS با Certificate موقت و HTTP خصوصی با Trust صریح، هر دو از `/health` پاسخ `۲۰۰` و از `/mcp` ناشناس پاسخ `۴۰۱` گرفتند؛ این تست ایزوله به معنی Rollout مشتری نیست.
- بررسی کل Solution: Build نسخه Release موفق بود و فقط یک Warning قدیمی `NU1603` در `SepidarGateway.Api` داشت؛ Buildهای متاثر Identity/MCP بدون Warning بودند.
- اتصال Auth در Identity: Scope/Permissionهای MCP Seed می‌شوند؛ فقط `identity.mcp.self.read` به Client عمومی پیش‌فرض داده می‌شود؛ Token کاربر فقط برای Membership یگانه یا پیش‌فرض Claim `tenant_id` می‌گیرد و Binding Tenant مربوط به M2M صریح است.
- Smoke پروتکل: `initialize` و `tools/list` روی `stdio` موفق بودند؛ دقیقاً ۳۶ Tool فعال کنترل‌شده (۳۴ Atomic و ۲ Business) منتشر شد و stdout فقط Frameهای MCP را داشت. پنج عملیات پروتکلی Binding صریح غیرقابل‌Expose دارند. در حالت stdio، Generic Host هیچ Listener مربوط به Kestrel باز نمی‌کند.
- برابری API/MCP: ۴۱ ورودی قابلیت API به‌صورت یک‌به‌یک Mapping شده‌اند؛ Grantهای API و MCP جدا هستند، هر مجوز فعال Authorization Tool قابل‌استفاده دارد و Permission جدید، Mapping/Tool یتیم یا Drift محدودیت‌ها از مسیر Validator و CI استاندارد Corevia Fail-closed می‌شود.
- پوشش امنیتی شامل استخراج server-owned برای Actor/Subject، رد Selector در Self، بررسی Scope قبل از Lookup در Admin، مرز Tenant، بررسی عضویت Subject در Delegation، Delegation امضاشده با Expiry/Audience/Nonce/Allowlist، Approval دقیق، bind شدن به Session جاری، اعتبارسنجی Principal و نبود Proxy عمومی است.
- کانفیگ Runtime deployment-neutral باقی مانده است: stdio برای Local/Offline، HTTP فقط با انتخاب صریح، و قواعد Loopback/Private/Public HTTPS enforce می‌شوند؛ Gateway مرکزی لازم نیست.
- اعتبارسنجی Remote CI/MR هنوز Gate نهایی Release این Branch است؛ Pilot مشتری، Publish پکیج، Deploy Production و کار آینده Gateway/AD/Keycloak خارج از این فاز هستند.
- یادداشت Validator مخزن: بررسی کل Repository، ۲۴ Finding قدیمی و نامرتبط در فایل‌های Legacy/Config/Documentation گزارش کرد و در فایل‌های تغییرکرده MCP Finding جدیدی نداشت؛ مالک پاک‌سازی Baseline، Platform Engineering است و این موضوع همچنان Gate خارجی Release محسوب می‌شود.
- یادداشت Documentation Audit: Audit کل Repository دو خطای Link قدیمی مربوط به Placeholder در `docs/templates/TECHNICAL_DOCUMENT_TEMPLATE.fa.md` و Warningهای Section قدیمی گزارش کرد؛ در Docs تغییرکرده Identity MCP خطای جدید Pair/Link وجود ندارد و Gate مرتبط MR همان Audit به‌صورت changed-only است.

## Validation / Verification

- Values مربوط به `roadmap.create-or-update` Schema و Link Validation را پاس کند.
- Values مربوط به `roadmap-phase.execute` پاس شود و فقط اولین فاز باز را پیدا کند.
- هر فاز اجرایی یک Outcome، Deliverable محدود، Task، Acceptance، Verification و Evidence داشته باشد.
- Roadmap، Decision Log، Specs، Changelog و Linkهای Index در نسخه انگلیسی/فارسی reciprocal و هم‌معنا باشند.
- هیچ Secret، Credential، Token یا داده مشتری خام در سورس، Docs، Values یا Evidence نباشد.
- Dependency Graph سورس Identity نبود وابستگی Application/Domain به Adapterهای MCP/API را ثابت کند.
- تست‌های Security-negative رفتار fail-closed را ثابت کنند.
- `scripts/docs/audit.sh` و Validator Corevia اجرا شوند؛ Warningهای قدیمی و نامرتبط جداگانه شناسایی شوند و به این رودمپ نسبت داده نشوند.

## Troubleshooting

- اگر Phase 1 Inventory کامل تولید نمی‌کند، فاز را باز نگه دارید و پروژه MCP را نسازید.
- اگر تصمیم معماری مرز Sibling، Scopeها، Actor/Subject، Modeهای Deployment یا MVP Exposure را تغییر می‌دهد، Decision Log را آپدیت و قبل از Implementation تصمیم جدید بگیرید.
- اگر Tool به Controller، Repository خام، EF Query یا Endpoint دلخواه نیاز دارد، متوقف شوید و Contract Application را بازطراحی کنید؛ Shortcut Transport اضافه نکنید.
- اگر HTTP محلی خارج از Loopback bind شد، Auth غیرفعال بود یا Forwarding بدون Authentication وجود داشت، آن Mode را Disable و `stdio` را مسیر Local نگه دارید.
- اگر Delegated Context محدودیت لازم یا Approval دقیق ندارد، Fail-Closed شوید و Denial redact‌شده ثبت کنید.
- اگر Gateway مرکزی، AD/LDAP، Keycloak، Consul، Vault یا Internet به Dependency اجباری Runtime تبدیل شد، آن را Architecture Drift بدانید و Decision باز کنید؛ dependency را بی‌صدا اضافه نکنید.
- اگر رفتار Version/Config/Deployment تغییر کرد، قبل از Release Version پروژه و محیط واقعی را همگام کنید.

## Update Policy

مجاز:

- افزودن یا شکستن فاز وقتی Outcome مستقل و قابل مشاهده دارد.
- به‌روزرسانی Status، Evidence، Tool Matrix، Referenceهای سورس و Notes پیاده‌سازی بر اساس Evidence تاییدشده.
- افزودن Tool فقط بعد از مشخص‌شدن Audience، Risk، Scope، Approval، Redaction، Mapping Application و تست.

نیازمند Decision صریح:

- اجباری‌کردن Gateway مرکزی برای هر Deployment فعلی.
- افزودن دسترسی مستقیم MCP به HTTP API/Controller، EF، Repository، SQL، AD/LDAP یا Secret خام.
- تغییر معنای Self/Admin/Security، قواعد Delegation امضاشده، نام Scopeها، مرز Tenant یا منبع Actor/Subject.
- Expose کردن OTP/Token Issuance، Client Secret، مواد امضا، Security Write یا Mutation حساس بدون Policy/Approval تاییدشده.
- جایگزین‌کردن Transport محلی `stdio`/Authenticated یا HTTPS سرور مشتری با Exposure عمومی و بدون Authentication.
- تغییر مدل Atomic/Business یا Bypass کردن قواعد Application/Domain.

## Metadata

- Last Updated: 2026-08-29
- Status: Active
- Version: 0.2.0

## Change Log

- 2026-08-13: ایجاد رودمپ استاندارد Corevia برای Adapter sibling MCP در IdentityService با مرزهای صریح معماری، امنیت، Tool، Deployment، Evidence و Integrationهای آینده.
- 2026-08-28: اولین Runtime واقعی `IdentityService.Mcp`، اجرای مستقیم Application/MediatR، تست‌های Security-negative، مرز Transportهای stdio/HTTP و شواهد اجرای محلی اضافه شد؛ Phase 7 تا زمان اعتبارسنجی Remote CI/MR باز می‌ماند.
- 2026-08-29: Baseline برابری API/MCP در Identity با ۴۱ Mapping یک‌به‌یک، ۳۶ Tool فعال، پنج Binding صریح غیرقابل‌Expose و Contractهای قابل‌استفاده مجدد Standard/Bridge تکمیل شد.

## Ownership

Product Engineering / IdentityService team
