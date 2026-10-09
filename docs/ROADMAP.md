# Roadmap

Persian mirror: `ROADMAP.fa.md`. Legend: `[ ]` todo, `[x]` done (note commit). Requirement IDs refer to `docs/specs/requirements.md`.

## Resume point
**Current phase:** phases 0-9, 10.1 (doctor) and 11.1-11.6 (adoption foundations) done; remaining open verification items: 1.9b, 2.9 (net9), 4.6 (real docker build), 5.4, 6.6 (live providers). Branch: `claude/exciting-fermi-b41fue`.
Environment: dotnet SDK 8 available (net9 targets cannot be built here; verify net9 in CI).

## Phase 0 - Docs, specs, rules, roadmap (R-E1, R-D1..D5)
- [x] 0.1 requirements (+fa), decisions (+fa), architecture trees, contracts, acceptance, overview, changelog
- [x] 0.2 AGENTS.md (spec/architecture/config/tool-code/roadmap/git rules)
- [x] 0.3 this roadmap (+fa)
- [x] 0.4 `openspec.yaml` / `testspec.yaml` (machine-readable spec of CLI + acceptance)
- [x] 0.5 dotnet SDK 8.0.131 installed via Microsoft apt repo (`packages.microsoft.com` and NuGet are reachable; SDK 9 is NOT available via apt, `builds.dotnet.microsoft.com` is blocked). Tool builds on net8.0.
- [x] 0.6 Fix: `DotNetArch.csproj` compiled `samples/**` (3508 errors); excluded samples/kits/tests

## Phase 1 - Tool restructure into Cli / Mcp / Core + hardening (R-E4, D-10, D-13, D-17, D-18)
- [x] 1.1 Fix spinner crash when console is not a TTY/width 0 (found: `new solution` non-interactive crashes at `ShowSpinner`)
- [x] 1.2 Golden baseline of the old tool stored in `tests/DotNetArch.Core.Tests/Golden/legacy-controller-none` (random ports/GUIDs must be normalised); new Cli output verified identical (24 files, only random ports differ)
- [x] 1.3 Skeleton for `src/DotNetArch.{Core,Cli}`, `Directory.Build.props`, new `DotNetArch.sln`; Cli packs as tool `DotNetArch` / `dotnet-arch`. (Mcp project is created in 7.1, test projects in 1.10, `Directory.Packages.props` when the first package is added)
- [x] 1.4 `ToolHost` + `IPrompter`/`IProcessRunner`/`IToolOutput` in Core; Console implementations in Cli; non-interactive prompter
- [x] 1.5 Move scaffolders/config/steps into Core (namespaces `DotNetArch.Core.*`), remove `Program.*` coupling
- [x] 1.6 Split `Main.cs` into Cli `Commands/*` (parsing only) calling Core operations
- [x] 1.7 `ProcessRunner` with argument lists (no `bash -c`/`cmd /c`), env map (fixes Windows `ASPNETCORE_ENVIRONMENT=` prefix)
- [x] 1.8 Identifier/path validation in Core.Validation, enforced inside Core entry points (solution, entity, event, enum, constant, action names, HTTP method, output path)
- [x] 1.9 Shared `MigrationService` (dedupe Crud/Action/exec/remove; no more `SetCurrentDirectory`)
- [ ] 1.9b Migrate static scaffolders (`CrudScaffolder` ...) to injected instances (D-18 follow-up)
- [x] 1.10 `DotNetArch.Core.Tests` (validation, command line, config round-trip, host, golden tree as Integration) and `DotNetArch.Cli.Tests` (arg parsing): 36 unit tests + 1 integration test pass
- [x] 1.10b Fix: `WaitForExit()` hung when orphaned MSBuild nodes kept the pipe open; node reuse disabled for `dotnet` children
- [x] 1.11a `.DS_Store` removed from the repo
- [x] 1.11b `.gitignore` extended; `scripts/smoke.sh` (build, unit, golden, generate+build) passes
- [x] 1.12 README run instructions updated; `CONTRIBUTING.md`, `SECURITY.md`
- [x] 1.13 Modern repo hygiene: `.editorconfig`, `.gitattributes`, analyzers + warnings-as-errors in CI/Release, SourceLink, central package management, snupkg symbols, GitHub Actions (`ci.yml` with 3-OS matrix + smoke + pack, `release.yml`), dependabot, PR/issue templates; installed-tool run verified from the packed nupkg
- [x] 1.14 Old root `Main.cs`, `Logger.cs`, `DotNetArch.csproj`, `runtimeconfig.template.json`, `.DS_Store`, root `Scaffolding/` removed (moved into `src/`)

## Phase 2 - Microservice core template v2 (R-A1..A8, D-10, D-12)
- [x] 2.1 `layout: v2` config key (`SolutionConfig.Layout`), legacy layout is the default when the key is absent; `new solution --layout=v2|legacy` (v2 default)
- [x] 2.2 `src/` skeleton: `Directory.Build.props`, `Directory.Packages.props` (central versions from `PackageCatalog`), `global.json`, `.editorconfig`, `.gitignore`, local `dotnet-ef` tool manifest, README
- [x] 2.3 Domain template: `Entity` base (identity, audit, domain events), `DomainException`, entities with private setters and behaviour (partial classes)
- [x] 2.4 Application template: ports (`IRepository`, `IUnitOfWork`, `IDomainEventDispatcher`), MediatR + FluentValidation pipeline, `AddApplication()`
- [x] 2.5 Infrastructure template: EF Core (SQLite/SqlServer/Postgres), async repository, unit of work, design-time factory, `AddInfrastructure()`
- [x] 2.6 Api template: short `Program.cs`, `AddApi()`/`UseApi()`, exception handler (problem details), controllers per entity folder, minimal-API variant (`--style=fast`)
- [x] 2.7 `new crud/action/event/enum/constant` emit nested per-entity slices (`Features/<Plural>/{Commands,Queries,Actions,Events,Dtos}`, `Domain/{Events,Enums,Constants}`)
- [x] 2.8 Verified: generated solutions (SQLite controller, Postgres fast, SqlServer dotted name) build warning-free in Release; SQLite API served create/validate/list/update/delete/404 end to end; 43 unit tests cover structure and rules
- [ ] 2.9 net9 target verification (no SDK 9 in this environment; versions in `PackageCatalog` are unverified for net9)
- [x] 2.10 `new service` for v2 (done in phase 6)

## Phase 3 - Configuration (R-A9, A10, D-09)
- [x] 3.1 `AddAppConfiguration()` (appsettings -> appsettings.{Env} -> .env -> .env.{env} -> environment -> command line; one step before options binding)
- [x] 3.2 Options with validation per layer (`DatabaseOptions` validated on start; secret key `DATABASE_CONNECTION_STRING` overrides, never in appsettings)
- [x] 3.3 `.env.example`, `appsettings.example.json`, `ConfigurationContract` (secret key inventory with a marker kits append to) and `scripts/validate-examples.sh`; the checking tests are generated in phase 5 (`Category=Configuration`)

## Phase 4 - Docker, Git, CI, registries (R-A11, A12, A15, D-06, D-07)
- [x] 4.1 `Dockerfile` (restore-cached multi-stage, non-root), `.dockerignore`, `docker-compose.yml` per database (SQLite volume / Postgres / SQL Server with health checks); compose validated with `docker compose config`
- [x] 4.2 Git setup incl. personal host/provider (`git setup --remote --git-host --git-provider`, stored as `git.host`/`git.provider`)
- [x] 4.3 CI provider detection (`GitHosts.Detect`) + GitHub Actions, GitLab CI, Azure Pipelines, Bitbucket Pipelines (+ Gitea Actions) templates; `--ci=auto|provider|none`; YAML validated by parsing
- [x] 4.4 Personal Docker registry + NuGet source (`--docker-registry`, `--nuget-source`, `--nuget-source-name`): `NuGet.config` without credentials (credentials by env/CI secrets), image names, CI login/push job, Dockerfile restore secret
- [x] 4.5 Commands `ci add`, `docker add`, `git setup`; new-solution flags `--no-docker`, `--no-git`; `exec --docker` works for v2
- [ ] 4.6 Real `docker build` / `docker compose up` verification (no Docker daemon in this environment; only config validation done)

## Phase 5 - Tests in generated projects (R-A14, D-02)
- [x] 5.1 Per-layer test projects (`Domain/Application/Infrastructure/Api.Tests`) with fakes (`FakeUnitOfWork`, `FixedTimeProvider`), SQLite test database, `ApiFactory` (WebApplicationFactory); `--no-tests` opt-out; added to the solution file
- [x] 5.2 Per-entity tests generated with every CRUD slice (domain, handlers + validators, persistence round trip, API CRUD flow for SQLite); configuration tests (`Category=Configuration`) keep `.env.example` / `appsettings.example.json` honest; CI templates run `dotnet test`
- [x] 5.3 Verified by running the generated suites: SQLite solution 69 tests pass (incl. API integration), Postgres solution 40 tests pass; Release build warning-free
- [ ] 5.4 `testspec.yaml` generation for generated projects (phase 8 templates)

## Phase 6 - Kits (R-B1..B8, D-05, D-08, D-11, D-14)
- [x] 6.1 Kit generator: `kits/<Area>/` with Abstractions, Core, one package per provider, own `Directory.Build.props` / `Directory.Packages.props` / `.sln`, `kit.json` metadata, README (+fa), AGENTS.md, specs (+fa), `scripts/pack.sh`; optional tests (`--with-tests`); no private package dependency (D-11)
- [x] 6.2 Built-in recipes verified by building (Release, warning-free) and running their tests: Cache (InMemory, Redis; 15 tests), MessageBroker (RabbitMq; 12 tests), MediaStorage (Minio, RustFs, ported from the sample; 16 tests). Custom areas get a generic skeleton. Redis/RabbitMQ/MinIO/RustFS were NOT verified against live servers
- [x] 6.3 `new service`: asks "Does this service contain business logic?" - yes: Application service (feature or common folder, DI registered via marker); no: kit generation + wiring. Flags for scripts/agents: `--logic --name --entity --lifetime` / `--area --providers --with-tests`. Legacy layout keeps the old flow
- [x] 6.4 `new kit` / `add kit`: Application references Abstractions only; Api composition root references Core + providers and registers them (`KitRegistrations`); appsettings(+example), `.env.example` and `ConfigurationContract` extended; idempotent; kit projects added to the solution file
- [x] 6.5 Kits in CI: `scripts/pack-kits.sh` + a `kits` job (all CI providers) pushing to the configured NuGet feed when `--nuget-source` is set; each kit has its own version and CI-friendly `scripts/pack.sh` (build, test, pack, push)
- [ ] 6.6 Live verification of provider kits against real servers (Redis, RabbitMQ, MinIO, RustFS) and of `docker build` with kits in the context

## Phase 7 - MCP (R-A13, R-C1, D-01)
- [x] 7.1 `DotNetArch.Mcp` layer + `dotnet-arch mcp serve` (stdio, official ModelContextProtocol SDK): 14 tools (`new_solution`, `new_crud`, `new_action`, `new_event`, `new_enum`, `new_constant`, `new_service`, `new_kit`, `add_kit`, `ci_add`, `docker_add`, `git_setup`, `list_entities`, `describe_config`); non-interactive host, results list created/modified files + equivalent CLI command, no destructive tools, logging to stderr; verified over the real stdio protocol (initialize, tools/list, create solution + CRUD) and by `DotNetArch.Mcp.Tests` (12 tests)
- [x] 7.2 `<App>.Mcp` host for generated projects (`new solution --mcp`, `add mcp`, MCP tool `add_mcp`): stateless streamable-HTTP MCP server, bearer-token auth (`MCP_AUTH_TOKEN`, constant-time compare), health, Dockerfile + compose override, config via the shared `AddAppConfiguration`; per-entity tools (`product_list|get|create|update|delete`) and per-action tools send the same MediatR requests as the controllers; tests generated (endpoint auth, handshake, tools). Verified live: create/list through MCP over HTTP; 6 generated MCP tests pass
- [x] 7.3 Config loading moved to Infrastructure (`AddAppConfiguration(IConfigurationBuilder)`) so Api and Mcp share it

## Phase 8 - Documentation completion (R-D1..D5)
- [x] 8.1 README (+fa) rewrite for new features; command reference
- [x] 8.2 Generated-project docs/specs/AGENTS templates; kit docs templates
- [x] 8.3 `.fa.md` mirrors for architecture, contracts, acceptance, overview, changelog
- [x] 8.4 bilingual-pair check script

## Phase 10 - Doctor (R-F1..F3)
- [x] 10.1 `DotNetArch.Core.Doctor` (read-only checks), `doctor` command (text/JSON, exit code 3), unit tests; run against 11 real service repos
- [ ] 10.2 Wire `doctor` into the golden/smoke script and CI
- [ ] 10.3 Kit grouping (generic / corevia / sepidar / ungrouped) - decision pending, intentionally not started
- [ ] 10.4 Shared `Mcp` kit (generic host + Corevia governance layer)

## Phase 11 - Adoption foundations (R-G1..G6, cycle zero)
- [x] 11.1 Decisions D-19..D-24 (independence, one registry, structure-only fixes, no private part, settings layers, mission)
- [x] 11.2 Operation registry (`doctor`, `adopt`, `fix`) feeding the CLI and the MCP server; mutating operations plan first (`--json`), write with `--apply`
- [x] 11.3 `.net-arch/` (project.yml, rules.yml, profile.yml, generated.lock), global `~/.net-arch/` (`DOTNET_ARCH_HOME`), rules.yml severity/thresholds/exceptions
- [x] 11.4 Declarative profile rules replace the organisation-specific checks (none remain in the tool)
- [x] 11.5 `adopt` (state from source, only `.net-arch/` written) and `fix` (hygiene files from tool templates), independence test
- [x] 11.6 Doctor: documentation validation (DA-M06..M09) and test checks (DA-T01..T02, coverage threshold)
- [x] 11.10 `fix` structural opt-in rules (DA-B03, DA-B07, DA-S04, DA-S06 layout v2 move with path rewriting); first real use: Catalog (506 tests pass, Release 0 warnings)
- [ ] 11.7 Move the existing generators onto the registry (one definition for CLI and MCP)
- [ ] 11.8 Independence smoke: generate, adopt, delete `.net-arch/`, build and test (needs SDK; add to `scripts/smoke.sh`)
- [ ] 11.9 Blueprint upgrade steps (versioned migrations); opt-in `format` fixer (the baseline of Catalog failed `dotnet format --verify-no-changes`)
- [x] 11.12 ABP alignment spec (`docs/specs/abp-alignment.md`); decisions accepted 2026-10-09 (D-25..D-29); implementation moved to Phase 12
- [ ] 11.11 `doctor`: lint baseline check (`dotnet format whitespace --verify-no-changes`) and OpenAPI export

## Phase 12 - ABP alignment (R-H1..H5, D-25..D-29)
- [x] 12.1 Specs and documentation updated from the ABP spec and the owner decisions (abp-alignment, D-25..D-29, R-H, AC-19..22, contracts, changelog)
- [x] 12.2 Layout v3 in the model: `adopt`/`doctor` accept `src/test/etc`; fix rule DA-A01 (v2 to v3) with tests (AC-19); first real use: Catalog (506 tests pass)
- [x] 12.3 Fix rule DA-A02, content-driven: layer projects only where files move in; references, packages, `InternalsVisibleTo`, Dockerfile restore lines and solution wired; blocked files listed; controllers move together; `--empty` for CLI users (AC-20); DA-A07 flags empty layer projects
- [x] 12.4 Built-in opt-in `abp` rule set DA-A01..DA-A06 in `doctor`, `standards: [abp]` in project.yml or profile, profile-level `severity` overrides (AC-21)
- [x] 12.5 Service-published files under `etc/`: no tool change needed; a profile requires them with the existing `require-files` kind (Corevia: CV-09), the tool names no product
- [x] 12.10 `fix` DA-A08: typed client project generated from the controllers (D-30, AC-23); Catalog: all 45 routes
- [x] 12.11 `fix` DA-A09: Compose files to `etc/docker/` with re-based paths (AC-24)
- [x] 12.12 `fix` DA-A10: folder tree inside the layers, typed client generated into it (D-31, AC-25)
- [x] 12.13 `fix` DA-A11: project references, solution and Dockerfile restore repaired (D-31, AC-26); this also covers 12.9
- [x] 12.6 (done in 13.4) Generator: `new solution --layout=v3 [--layers=abp]` on the registry (with 11.7) (AC-22)
- [x] 12.7 File-move fixer: part of DA-A02 (type-name dependency closure, pull-in of self-contained data types, blocked files reported); proven on Catalog (tool alone on the v2 state: build 0 warnings, 506 tests pass; HttpApi waits for one split interface)
- [x] 12.9 `doctor` check: the Dockerfile restore stage copies every project reachable from the host (DA-A02 now writes the lines for the layers it creates; the check protects hand-made changes)
- [ ] 12.8 Independence smoke on v3 (generate, adopt, delete `.net-arch/`, build and test)

## Phase 13 - One registry for every command (R-I1..I4, D-32..D-34)
- [x] 13.1 Generators as registry operations; CLI words and MCP tools derive from one definition; plan-first via a throw-away copy (AC-27, AC-28)
- [x] 13.2 New operations: `add_layer`, `add_tests`, `spec_list|add|check`, `graph`; `adopt --standards` (AC-30)
- [x] 13.3 ABP fixers run after every generator in a v3 project; templates made fixer-friendly (DTO/mapping split, async `RemoveAsync`, partial controller parts travel together) (AC-29)
- [x] 13.4 `new solution` defaults to layout v3 (closes 12.6); generated solution with crud, action and event builds with 0 warnings, passes tests, no ABP finding
- [x] 13.5 Typed client tests generated with the client (verb and route of every action, route parity); a client follows new controller routes (partial controller files included), so an added action reaches the client and its tests
- [ ] 13.6 Smoke on the packaged tool: install, generate, adopt, delete `.net-arch/`, build and test (also closes 12.8)
- [ ] 13.7 Interactive confirmation after the plan covers `exec` and `remove migration` end to end on a real database

## Phase 9 - Verification and release
- [x] 9.1 Run smoke script end to end (needs SDK); fix findings (smoke OK, v1.3.0)
- [x] 9.2 Version bump, changelog, final review (smoke OK, v1.3.0)
