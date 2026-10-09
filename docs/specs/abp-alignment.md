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
2. **Layout v3 = ABP-shaped folders** `src/`, `test/` (singular), `etc/` (D-26). v2 (`tests/`) stays valid for services already adopted; `fix --rules=DA-A01` moves v2 to v3.
3. **Layer projects** `Domain.Shared`, `Application.Contracts`, `HttpApi`, `HttpApi.Client` are *optional for the tool and mandatory for a profile* (D-27), and **a layer exists only where there is something for it**: `fix --rules=DA-A02` creates a layer project only together with the files that move into it (enum-only files to `Domain.Shared`; DTOs and MediatR requests to `Application.Contracts`; controllers to `HttpApi`), keeping namespaces and the path inside the project, so no code is edited. A human on the CLI may ask for empty layers with `--empty` (they own the development plan); an agent over MCP gets the default: migrate what belongs, or do nothing.
4. **A built-in, opt-in `abp` rule set in `doctor`** (D-28), ids `DA-A01..DA-A09`, enabled by `standards: [abp]` in `project.yml` or in a profile; a profile can also raise the severity of any rule id (`severity:` in the profile), so an organisation can require the layer projects while the tool only warns. It contains only structural rules; it has no rule about `Guid` ids or application services, so CQRS services with business ids need no exception.
5. **Services own what they publish** (D-29): files a service publishes for other systems (for example its gateway route declaration) live in the service under `etc/` and consumers aggregate them; a service never depends on a consumer. Which file and which consumer is organisation-level knowledge and belongs to a profile.

## 6. Resulting rules
| Id | Rule | Kind |
|---|---|---|
| DA-A01 | Layout v3: projects under `src/`, test projects under `test/`; `etc/` is the place for non-code files a service publishes and is optional | structure |
| DA-A02 | A layer project exists wherever files belong in it (enum-only files, DTOs and requests, controllers) | structure |
| DA-A07 | A layer project without any source file must not exist | structure |
| DA-A08 | A service with controllers has a typed client project (`HttpApi.Client`) | structure |
| DA-A09 | Compose files live in `etc/docker/`, not at the repository root | structure |
| DA-A03 | Reference direction: Domain.Shared none; Domain to Domain.Shared; Contracts to Domain.Shared; HttpApi and HttpApi.Client to Contracts only (the persistence project may reference Application because ports live there) | structure |
| DA-A04 | Repository interfaces: async methods, optional `CancellationToken` last, no `IQueryable` returned | code |
| DA-A05 | Domain services end with `Manager`; application service interfaces end with `AppService` and live in Contracts (only when such types exist) | code |
| DA-A06 | DTO types live in `Application.Contracts` (only when that project exists) | code |

## 7. Fix and migration path
- `fix --rules=DA-A01`: v2 to v3 (flat layouts run DA-S06 first) (`tests/` to `test/`, rewriting every path that points at it, same machinery as DA-S06).
- `fix --rules=DA-A02`: analyse the source, create the layer projects that have something to receive, move the files (namespaces and the path inside the project unchanged), add the references (Domain to Domain.Shared, Application to Application.Contracts, the host to HttpApi), copy the packages, framework reference and `InternalsVisibleTo` entries the moved files need, list the new projects in the Dockerfile restore stage, and register them in the solution.
- The analysis pulls in the self-contained data types a moved file needs (records, enums, HTTP models in `Contracts/`, `Models/`, `Dtos/` folders) and keeps back every file whose dependencies cannot move with it; each such file is listed as manual with the blocking file. Controllers move together: one blocked controller keeps all of them, because a host split over two assemblies breaks route discovery.
- The dependency analysis is textual (type names), not a compiler: a name used as a type counts as a dependency, member and parameter names do not. The migration step must therefore be followed by build and tests; a service that fails them is reverted, not patched.
- `fix --rules=DA-A08`: generate the typed client `HttpApi.Client` from the controllers: one interface and one class per controller over `HttpClient` and `System.Net.Http.Json`, using only types from Application.Contracts / Domain.Shared (the API request and response models therefore belong to Application.Contracts; a model that needs ASP.NET Core stays with the controllers). It is new code, not a move; an action it cannot express (a type outside the contracts, a body or form source it does not know) is listed as manual with the reason. Run DA-A02 first.
- `fix --rules=DA-A09`: move root Compose files to `etc/docker/`, re-base their relative paths (build context, env files, bind mounts) and update mentions of the file name outside history documents.
