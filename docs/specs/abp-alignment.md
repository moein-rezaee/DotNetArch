[فارسی](./abp-alignment.fa.md)

# ABP standards: extracted spec and distance from DotNetArch

Purpose: take the published ABP Framework conventions as the reference standard, measure how far DotNetArch (layout v2 and the Corevia services) is from it, and record what is adopted. Status: **accepted by the owner on 2026-10-09** (decisions D-25..D-29); the implementation plan is roadmap Phase 12.

## 1. Sources
All rules below were read from the official ABP documentation (version "latest", checked 2026-10-09):
[Module architecture](https://abp.io/docs/latest/framework/architecture/best-practices/module-architecture), [Entities](https://abp.io/docs/latest/framework/architecture/best-practices/entities), [Repositories](https://abp.io/docs/latest/framework/architecture/best-practices/repositories), [Domain services](https://abp.io/docs/latest/framework/architecture/best-practices/domain-services), [Application services](https://abp.io/docs/latest/framework/architecture/best-practices/application-services), [DTOs](https://abp.io/docs/latest/framework/architecture/best-practices/data-transfer-objects), [EF Core integration](https://abp.io/docs/latest/framework/architecture/best-practices/entity-framework-core-integration), [Layered solution structure](https://abp.io/docs/latest/solution-templates/layered-web-application/solution-structure), [Microservice solution structure](https://abp.io/docs/latest/solution-templates/microservice/solution-structure).

## 2. ABP standards
### 2.1 Solution and packages
- Naming `CompanyName.ModuleName.<Layer>`; one solution per module; every package declares its dependencies.
- Root: `src/`, `test/` (singular), `etc/` (`docker/`, `helm/`, `scripts/`, `abp-studio/`), `common.props`, `NuGet.Config`.
- Packages and allowed references:

| Package | Contains | References |
|---|---|---|
| `Domain.Shared` | constants, enums, shareable types (no entities) | none |
| `Domain` | entities, value objects, repository interfaces, domain services | Domain.Shared |
| `Application.Contracts` | `I...AppService` interfaces and DTOs | Domain.Shared |
| `Application` | application service implementations | Domain, Application.Contracts |
| `EntityFrameworkCore` / `MongoDB` | DbContext, repository implementations (replaceable) | Domain only |
| `HttpApi` | REST controllers, one per application service | Application.Contracts only |
| `HttpApi.Client` | remote client proxies | Application.Contracts only |
| `Web` / `HttpApi.Host` / `DbMigrator` | hosts and tools | HttpApi, Application, EF |

- Usage patterns: monolith (Web + Application + EF), microservice (HttpApi + Application + EF), UI + remote (Web + HttpApi.Client), client consumer (HttpApi.Client), API proxy (HttpApi + HttpApi.Client).
- Tests under `test/`: `Domain.Tests`, `Application.Tests`, `EntityFrameworkCore.Tests`, `Web.Tests`, `TestBase`.
- Microservice template: mono-repo `apps/`, `gateways/`, `services/`, `etc/`; a plain service is 2 projects (host + `.Contracts`), a service with real business logic is a layered module hosted by a small `HttpApi.Host`.

### 2.2 Rules per area
- Entities: aggregate roots with a single `Guid` id passed in (never generated inside), small aggregates, public/protected primary constructor that validates, protected parameterless constructor for the ORM, virtual members, private setters with guarded methods, other aggregates referenced by id only.
- Repositories: interface in the Domain, only for aggregate roots, specific names (not generic `IRepository<T>` in application code), all methods async with an optional `cancellationToken`, `includeDetails` flags, no `IQueryable` exposed.
- Domain services: `Manager` suffix, no interface unless needed, no getters, state-changing and intention-named methods, `BusinessException` with namespaced error codes, never return DTOs, no current-user logic.
- Application services: one per aggregate root, interface in Contracts with the `AppService` suffix, `Async` methods named `GetAsync`, `GetListAsync`, `CreateAsync`, `UpdateAsync(id, dto)`, `DeleteAsync`, never take or return entities, separate input DTOs per method, data annotations for validation, no LINQ in services (use repositories), never call other application services of the same module, no web types such as `IFormFile`.
- DTOs: in Contracts, serializable, public get/set, validation attributes, no logic.
- EF Core: a DbContext interface and class (`AbpDbContext<T>`), `TablePrefix`/`Schema` constants, mapping through `Configure[Module]` extension methods with `ConfigureByConvention`, repositories derive from `EfCoreRepository`, `IncludeDetails` extension methods.

## 3. Distance of DotNetArch (layout v2) from ABP
| Area | ABP | DotNetArch v2 | Distance |
|---|---|---|---|
| Layers | 8-10 packages incl. Domain.Shared, Application.Contracts, HttpApi, HttpApi.Client | Domain, Application, Infrastructure, Api (+ Mcp, optional Contracts, optional `Infrastructure.<Provider>`) | large: no Domain.Shared, no Contracts/Application split, no HttpApi/Host split, no client package |
| Folders | `src/`, `test/`, `etc/` | `src/`, `tests/` (plural), no `etc/` | small (rename and add `etc/`) |
| Dependency direction | HttpApi to Contracts only | Api references Application and Infrastructure (composition root) | different style, both inward-pointing |
| Application style | one application service per aggregate | CQRS with MediatR, one use case per folder | different paradigm |
| Entities | Guid ids, protected ctor, virtual members | private setters and behaviour; id type not prescribed | medium |
| Repositories | per aggregate root, async, `includeDetails`, no `IQueryable` | async repositories without `IQueryable`; generic `IRepository<T>` plus unit of work | medium |
| Domain services | `Manager` suffix, `BusinessException` | not generated | gap |
| DTOs | in Contracts | in `Features/<Plural>/Dtos` inside Application | medium |
| EF Core | DbContext interface, prefix/schema, `Configure[Module]` | EF Core in Infrastructure, no interface or prefix | medium |
| Tests | `test/` with Domain, Application, EF tests | per layer under `tests/` (+ Domain tests via `fix`) | small |
| Runtime framework (modules, multi-tenancy, audit, permissions, localization, settings) | built in | not in the tool (belongs to Kits) | out of scope |
| Docs | module documentation | bilingual specs, roadmap, decisions, evidence | DotNetArch is ahead |

## 4. Distance of the Corevia services
Measured on Catalog after the layout v2 move: it already has `src/` and the separate `Contracts`/provider projects; it lacks Domain.Shared, Application.Contracts for services, HttpApi/Client packages and `etc/`; it uses CQRS handlers, not application services; ids are business ids (not `Guid`); persistence is not EF Core. The Corevia services therefore sit at "ABP-shaped folders, different internals".

## 5. Decisions (owner, 2026-10-09)
1. **ABP is the structural reference standard** of the tool (D-25). The ABP runtime framework (module system, multi-tenancy, audit, permissions, localization, settings) is not copied; it stays in Kits.
2. **Layout v3 = ABP-shaped folders** `src/`, `test/` (singular), `etc/` (D-26). v2 (`tests/`) stays valid for services already adopted; `fix --rules=DA-S07` moves v2 to v3.
3. **Layer projects** `Domain.Shared`, `Application.Contracts`, `HttpApi`, `HttpApi.Client` are *optional for the tool and mandatory for a profile* (D-27). The tool creates the empty projects with the allowed references; it never moves types between projects. Moving types is a reviewed migration step (namespaces stay unchanged, so no code is edited).
4. **A built-in, opt-in `abp` rule set in `doctor`** (D-28), ids `DA-A01..DA-A06`, enabled by `standards: [abp]` in `project.yml`. It contains only structural rules; it has no rule about `Guid` ids or application services, so CQRS services with business ids need no exception.
5. **Services own what they publish** (D-29): files a service publishes for other systems (for example its gateway route declaration) live in the service under `etc/` and consumers aggregate them; a service never depends on a consumer. Which file and which consumer is organisation-level knowledge and belongs to a profile.

## 6. Resulting rules
| Id | Rule | Kind |
|---|---|---|
| DA-A01 | Solution roots are `src/`, `test/`, `etc/` (layout v3) | structure |
| DA-A02 | Layer projects `Domain.Shared`, `Application.Contracts`, `HttpApi`, `HttpApi.Client` exist | structure |
| DA-A03 | Reference direction: Domain.Shared none; Domain to Domain.Shared; Contracts to Domain.Shared; HttpApi and HttpApi.Client to Contracts only; persistence to Domain only | structure |
| DA-A04 | Repository interfaces: async methods, optional `CancellationToken` last, no `IQueryable` returned | code |
| DA-A05 | Domain services end with `Manager`; application service interfaces end with `AppService` and live in Contracts (only when such types exist) | code |
| DA-A06 | DTO types live in `Application.Contracts` (only when that project exists) | code |

## 7. Fix and migration path
- `fix --rules=DA-S07`: v2 to v3 (`tests/` to `test/`, rewriting every path that points at it, same machinery as DA-S06).
- `fix --rules=DA-S08`: create the four layer projects with the allowed references and register them in the solution (structure only).
- Moving DTOs, constants and controllers into the new projects is a migration step done by the service owner or agent, with the namespaces unchanged; `doctor` DA-A02..A06 shows what remains. A file-move fixer is considered only after a real service has proven the pattern (roadmap 12.7).
