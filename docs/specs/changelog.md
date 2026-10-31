# Changelog

## 2.0.0 (unreleased)
- DA-AD09 and `add_adapter` (R-K9, AC-62): outbound HTTP leaves the shared Infrastructure for an adapter project the tool creates.
- `split_registration --also=Name,...` moves statements that name a kit extension method too; DA-AD09 ignores comments.
- `move_code` adds a reference to the target in each project (tests, hosts) that names the moved code; `split_registration` keeps the usings of the adapter namespace, drops them from the shared file, and moves private helpers and packages.
- `describe_module` lists the permissions a module declares in its own permission catalog (R-K8, AC-61).
- `split_registration` (R-K7, AC-60): the registration method of the shared Infrastructure is split per adapter by the tool.
- DA-AD08: the core projects an adapter depends on are packages at the module version (R-K6, AC-59).
- `scaffold_adapter` and the adapters rule DA-AD07 (R-K5, AC-58).
- Composable modules (R-K1..R-K4, AC-54..AC-57): `move_code`, `add_composition`, `describe_module`, `describe_settings` and the adapters rule set DA-AD01..DA-AD06 (profile keys `adapter_package` and `non_adapters`). Proven on four Sepidar services (Catalog, Customer, Order, Invoice): every existing test passes unchanged.
- Migration files follow the editorconfig (R-J22, AC-53): the byte order mark EF writes is removed so the lint job passes on a new service.
- Tenancy over an external system of record (R-J21, AC-52): `add_tenancy --wire_only` wires only the tenancy kit.
- Kit creation (R-J20, AC-51): `scaffold_kit` and `scaffold_provider` create Kit areas and their provider packages with tests, bilingual docs and the package-list entry; `fix --rules=DA-A08` skips controllers hidden from the API description (`IgnoreApi = true`). 35 operations, CLI and MCP from one registry. Used to create the Delivery and Chat Kits.
- Documentation and version gates (R-J19, AC-50): doctor DA-M10 (language links of a pair) and DA-B10 (the Api `<Version>`) with fixers; the README and docs templates carry the links, the standard README sections and the version.
- Legacy services (R-J15..R-J18, AC-46..AC-49): `dotnet-arch.yml` derived from disk (DA-G01), `add_persistence` (the standard persistence skeleton for a service with no database), `add_tenancy` (multi-tenant over a shared profile; doctor DA-TN01, opt-in), fixes for DA-K04 and for DA-M09 without an application test host, a hand-written MCP host is left alone, plans compare content; 33 operations. First used on Payment, Notification and Basket.
- `fix --rules=DA-D03` runs the final stage of a .NET runtime Dockerfile as the non-root app user (R-J14, AC-45).
- `fix --rules=DA-BP01` derives the blueprint of a service that has none (R-J13, AC-44).
- Doctor and fix: the typed client expresses a GET that binds a query record, an ignored `.env` is not a finding, DA-M09 is fixed by an OpenAPI snapshot test, and a blueprint restores recorded lengths (R-J12, AC-43).
- Blueprints are versioned (`schema`, `version`, `.net-arch/service-blueprint.lock`); the doctor checks them (DA-BP01..05) and `fix` applies them (R-J11, AC-42).
- The generated repository port pages newest first and counts (`newestFirst`, `CountAsync`; R-J10, AC-41).
- `apply_blueprint` (generate what a service blueprint describes, in one plan and repeatable, R-J9, AC-40); 31 operations. The length of a typed string input is recorded in `dotnet-arch.yml`, and a use case may return the DTO of another entity.
- Typed inputs take a validated length (`Name:string(8000)`), and a feature controller already in the HttpApi layer is not declared again in the Api project (R-J8, AC-39). The sample `Dm.LicenseGenerator` licence service (`corevia-licensing`) was the first service to need both.
- `apply_profile` (wire or upgrade a profile's external kits in an existing solution, R-J7); 30 operations before it. String samples respect the declared length. Generated tests: the Api test accepts 404 or 401 for an unknown route (a host with a fallback authorization policy answers 401) and the Infrastructure DI test no longer validates the whole graph on build (kit and host services are registered by the Api).
- `new entity` property flag `~` keeps a property out of the DTO; typed use cases that name an enum get the enum namespace.
- The generated Dockerfile no longer starts with a `# syntax=` line (the default BuildKit frontend covers `RUN --mount`; the line needs the frontend image, which an offline or private registry may not have).
- `new entity` (typed properties and persistence without CRUD), `new feature` (use-case groups without an entity), `new enum --members`, repository `FirstOrDefaultAsync` / `ListAsync`, `DomainException.OptionalText` (R-J5, R-J6); 29 operations.
- Profile `external_kits` and `new solution --profile`: published kits are wired into the Api, MCP and CLI hosts from data, replacing the hand-rolled Swagger and exception-handler code when the kit says so (R-J4, D-39).
- `new solution --in-place`; typed use cases (`new action --input --output --anonymous --description`, recorded as `usecase.*` in `dotnet-arch.yml`); `add_cli` and `new solution --cli`: a CLI host that is a peer of the MCP host, with a parity test (R-J1..R-J3, D-36..D-38); 27 operations. Fixed: an unused variable that stopped Release builds of the tool.
- DA-A13 (doctor warning in the `abp` set, fixer `fix --rules=DA-A13`): every project folder sits directly under `src/` (or the tests root) with the exact name of its project; nested folders (`src/api/App.Api`, `src/Extensions/App/App.Core`) and wrong case move, and project references (recomputed), the solution, Dockerfiles (including `COPY --from=build /src/src/...`), Compose, CI, scripts and Markdown guides follow. New layer projects carry the `LangVersion` the repository pins; a layer test for a .NET Framework project silences the cross-framework version notice (MSB3277/NU1701) and gets central versions for the test packages when the repository uses them and had none.
- Found while migrating the nine .NET services: the layer fixer carries the packages the moved files name by namespace (for example `Microsoft.EntityFrameworkCore`, `Corevia.Kit.*`) to the new layer project; an extension-method call counts as a dependency (a controller that calls one stays); a comment between the attributes and an action no longer hides the action from the typed client; the client project's transport package gets the Kit version (central or direct); the doctor skips folders it cannot read.
- **Breaking** (D-32..D-34): the generators are registry operations. Every file-changing command plans first and writes only with `--apply` (MCP: `apply: true`); the old immediate behaviour is in 1.3.x. MCP tools take the registry values (`path`, `entity`, ...), not the old camelCase names (`solutionPath`, ...). `new solution` defaults to layout v3 (ABP); `--layout=v2|legacy` remain.
- One registry for all commands: CLI words (`new crud`) and MCP tools (`new_crud`) derive from it; 26 operations. New: `add_layer`, `add_tests`, `spec_list`, `spec_add`, `spec_check`, `graph`; `adopt --standards=abp`.
- In an ABP project every generator is followed by the ABP fixers (DA-A01, A02, A10, A11, A08, A09) and an `.net-arch/` refresh; a generated solution with entities, actions and events builds without warnings, passes its tests and has no ABP finding.
- Templates: DTOs are pure data, entity mapping is a separate file, repository ports are async (`RemoveAsync`) and return no `IQueryable`; the tests root follows the layout (`tests` or `test`).
- The typed client is generated with its tests and follows later controller routes (R-I5); the framework of generated projects comes from Directory.Build.props when the projects do not name one.
- Profile `folder_moves` and `fix --rules=DA-A12` (D-35); DA-A09 leaves Compose mentions of another repository alone.
- Doctor: DA-A10, DA-A11 (see Unreleased list below); `IQueryable` check ignores comments.

## Unreleased (folded into 2.0.0)
- New `dotnet-arch doctor` (read-only diagnosis: layers, dependency direction, tests and coverage, build hygiene, configuration and secrets, Docker/CI, documentation, code rules, kit boundaries; text or `--json`; exit code 3 when blocking).
- New `dotnet-arch adopt` (brings an existing project under control by writing only `.net-arch/`) and `dotnet-arch fix` (mechanical hygiene fixes, plan first, `--apply` to write).
- New `.net-arch/` folder (`project.yml`, `rules.yml`, `profile.yml`, `generated.lock`) with a global `~/.net-arch/`; rules.yml carries severity overrides, thresholds and exceptions with a reason.
- Organisation rules are a declarative profile (`profile.yml`); the tool itself contains no organisation-specific check.
- One operation registry feeds the CLI and the MCP server (3 registry tools: `doctor`, `adopt`, `fix`; 18 tools in total).
- `fix` opt-in structural rules: DA-B03 central package versions, DA-B07 warnings as errors, DA-S04 Domain test project, DA-S06 layout v2 move (projects to `src/`, tests to `tests/`, every pointing path rewritten); the default `.editorconfig` fix is now a rule-less marker so lint keeps passing on existing code.
- Doctor: documentation validation (index, specs, OpenAPI snapshot) and test checks (tests present, coverage versus threshold).
- ABP alignment accepted (D-25..D-29, `abp-alignment.md`): layout v3 `src/test/etc`, optional layer projects, opt-in `abp` rule set in `doctor`, `fix` rules DA-A01 and content-driven DA-A02 (moves the files that belong in `Domain.Shared`, `Application.Contracts`, `HttpApi`, wires references/packages/Dockerfile; empty layers only with `--empty`), `fix` DA-A08 (typed HTTP client generated from the controllers, over `HttpClient` or over a REST client abstraction a profile names) and DA-A09 (Compose files to `etc/docker/`), DA-A10 (folder tree inside the layers, D-31) and DA-A11 (project references, solution, Dockerfile restore), `new solution --layout=v3` (implementation tracked in roadmap Phase 12).

## 1.3.0
- Tool restructured into `DotNetArch.Core` (implementation), `DotNetArch.Cli` (commands) and `DotNetArch.Mcp` (MCP server) with per-layer tests; process execution without a shell; identifier validation.
- New layout v2 microservice template: Domain/Application/Infrastructure/Api, vertical slices with CQRS (MediatR), unit of work and repository, central package management, configuration loading, tests per layer.
- `new service` generates an application service or an independent Kit; kit recipes for Cache, MessageBroker, MediaStorage and generic areas; `new kit`, `add kit`.
- Docker, Git (personal hosts), CI for GitHub/GitLab/Azure/Bitbucket/Gitea, private Docker registry and NuGet feed; `ci add`, `docker add`, `git setup`.
- MCP: `dotnet-arch mcp serve` with 15 tools; generated MCP host (`--mcp`, `add mcp`).
- Generated projects and kits ship AGENTS.md, specs, roadmap and decision log in English and Persian.
- Fixed: spinner crash without a terminal, build of the tool picking up `samples/`.

## 1.2.0
- Existing released version (see git history).
