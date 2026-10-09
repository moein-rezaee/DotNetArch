[فارسی](./abp-alignment.fa.md)

# ABP standards: extracted spec and distance from DotNetArch

Purpose: take the published ABP Framework conventions as the reference standard, measure how far DotNetArch (layout v2 and the Corevia services) is from it, and decide what to adopt now. Nothing in this document changes the tool; decisions are listed at the end.

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

## 5. Recommendation
Adopt the ABP conventions that are structural and cheap, keep the parts that depend on ABP's runtime framework out:
1. Make layout names configurable and align the defaults with ABP: `src/`, `test/`, `etc/`.
2. Add an optional layered blueprint (`--layout=layered`) with `Domain.Shared`, `Application.Contracts`, `HttpApi`, `HttpApi.Client` and a thin host, because typed clients and shared contracts are what Corevia services lack today; keep v2 for existing services.
3. Add ABP naming and repository rules to `doctor` as an opt-in profile (async plus cancellation token, no `IQueryable`, `Manager` suffix, `AppService` interface suffix, DTOs in Contracts) so adoption is measurable.
4. Do not copy the runtime module system, multi-tenancy or localization into the tool; keep them in Kits.
5. Do not force Guid ids or application services onto services that use business ids and CQRS: record them as accepted exceptions of the Corevia profile.

## 6. Decisions needed
- Folder name `test` (ABP) versus `tests` (current): changing it is one tool rule plus a move per adopted service (Catalog was moved to `tests/` on 2026-10-09).
- Whether the layered blueprint is part of the next cycle or a separate roadmap phase.
- Whether the ABP rules become the default profile of the tool or stay opt-in.
