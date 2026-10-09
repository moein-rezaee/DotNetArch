# DotNetArch - Requirements (source of truth)

Every statement the owner gave is recorded here with a stable ID. Nothing may be dropped or
weakened without an entry in `docs/decisions/decisions.md`. Persian mirror: `requirements.fa.md`.

Status legend: `[ ]` not started, `[~]` in progress, `[x]` done and verified. Status is tracked in
`docs/ROADMAP.md`, not here.

## A. Generated microservice (target architecture)

- R-A1. Clean Architecture; layers are separate projects.
- R-A2. Ports and Adapters: Application/Domain own the ports (interfaces); Infrastructure/Api own adapters.
- R-A3. Vertical Slice + CQRS + Unit of Work + Repository pattern combined.
- R-A4. Queries, Commands and Actions are nested folders inside their own entity folder (one entity = one feature folder).
- R-A5. CQRS dispatching uses MediatR, as in the reference sample (see D-04).
- R-A6. Follow Clean Architecture, Clean Code and SOLID.
- R-A7. Correct wiring: each layer registers its own dependencies through its own DI extension (`AddXxxDomain/Application/Infrastructure/...`); the composition root only calls them.
- R-A8. Package (NuGet) references follow the same rule: each layer references only the packages it needs; no package leaks to a layer that does not use it.
- R-A9. Configuration: environment variables hold secrets and run-time values; `appsettings.json` holds non-sensitive settings. Both are loaded into `IConfiguration` when the project loads (single configuration-load step).
- R-A10. `.env.example` and `appsettings.example.json` are generated and kept in sync with the code.
- R-A11. Docker support (Dockerfile, compose, .dockerignore).
- R-A12. Git support (init, .gitignore, initial commit, remote/host configuration).
- R-A13. MCP support in the generated project: an MCP host whose tools mirror the project's controllers/services (same Application commands/queries, no duplicated logic).
- R-A14. Test support: a test project per layer, generated ONLY for generated projects (not for the DotNetArch tool itself, see D-02).
- R-A15. CI support: the tool picks the matching CI option itself (see D-06); supports personal/self-hosted git hosts; supports a personal Docker registry and a personal NuGet feed.

## B. `new service` (external services become Kits)

- R-B1. Two modes exist. Internal service (has business logic): keep current behaviour (correct as is).
- R-B2. External service (no business logic): the current behaviour is WRONG and is replaced by Kit generation.
- R-B3. A Kit has: an Abstractions package (clean-code/clean-architecture contracts), a Core package (options binding, provider selection, shared implementation, single `AddXxx(configuration)` entry point) and N Provider packages (can grow or shrink).
- R-B4. Kits are named after the capability, never after the product: `Cache` (providers: Redis, InMemory...), `MessageBroker` (provider: RabbitMq...), `MediaStorage` (providers: Minio, RustFs). No "Redis kit" or "RabbitMQ kit".
- R-B5. The reference kit is `samples/MediaStorage`; its structure, docs and rules are the template.
- R-B6. Kits are completely independent: their own versioning, buildable in CI, pushable to a personal NuGet registry, consumable by any service.
- R-B7. Kits live in one folder (`kits/`), grouped by business area: `kits/MediaStorage`, `kits/Cache`, `kits/MessageBroker`.
- R-B8. A kit is added to a service through its Abstractions in Application, its Core + Providers in the composition root only.

## C. DotNetArch tool as an MCP server

- R-C1. A separate MCP mode for using the library/tool itself: an agent can create solutions, entities, CRUD, actions, events, enums, constants, services/kits through MCP tools. (Distinct from R-A13.)

## D. Documentation, specs, rules

- R-D1. Complete documentation for the tool, the generated project and the kits.
- R-D2. Spec-driven development: specs (`openspec.yaml`, `testspec.yaml`, overview/contracts/acceptance/changelog) exist in the tool repo, generated projects and kits; behaviour changes update specs in the same change.
- R-D3. Agent-driven development: `AGENTS.md` with rules and best practices in the tool repo, generated projects and kits.
- R-D4. Bilingual pairs (`.md` + `.fa.md`) kept in sync, like the sample.
- R-D5. Best-practice rules follow the owner's description, NOT the reference sample blindly (see D-10).

## E. Process

- R-E1. Documentation, specs, rules and roadmap come FIRST; no decision and no point may be lost.
- R-E2. Roadmap lives in the project, phased; every executed item is ticked; if credit runs out, the roadmap shows the exact resume point.
- R-E3. Work proceeds phase by phase; each phase is committed and pushed to `claude/exciting-fermi-b41fue`.
- R-E4. Fix the sample's defects found during review instead of copying them (see D-10).
- R-E5. Show the final tree to the owner (`docs/specs/architecture.md`).

## F. Doctor (existing repositories)

- R-F1. `dotnet-arch doctor [path]` diagnoses an existing repository against the standard without changing it: layers and dependency direction, test projects, build hygiene, configuration and secrets, Docker/CI, bilingual docs, code rules and kit boundaries.
- R-F2. Organisation rules come from a declarative profile (`.net-arch/profile.yml` or a shared file it references); the tool contains no organisation-specific check. A profile may accept other layouts than v2.
- R-F3. Output for humans (text) and agents (`--json`, MCP tool `doctor`); findings carry id, severity, locations and a fix hint. Exit code `3` when blocking (errors, or warnings with `--strict`).

## G. Adoption and independence

- R-G1. `adopt` brings an existing project under tool control by writing only `.net-arch/` (state derived from the source, rules, profile reference, generated.lock); it changes no source file and is repeatable.
- R-G2. The project never depends on the tool: deleting `.net-arch/` breaks neither build, tests, Docker, CI, documentation nor MCP.
- R-G3. `fix` restores the standard only where the change is mechanical and cannot alter behaviour (hygiene files, ignore lines); it plans first and writes only with `--apply`.
- R-G4. One operation registry defines every operation once; CLI and MCP derive from it. Mutating operations return a machine-readable plan.
- R-G5. Settings: tool defaults < global `~/.net-arch/` < project `.net-arch/`; rules.yml carries severity overrides, thresholds and exceptions with a mandatory reason.
- R-G6. `doctor` also validates documentation (index, specs, OpenAPI snapshot) and tests (tests present, coverage versus threshold).

### ABP alignment (R-H)
- R-H1. Layout v3 (`src/`, `test/`, `etc/`) is supported by `adopt`, `doctor` and `fix`; v2 stays valid; `fix --rules=DA-A01` moves v2 to v3 and rewrites every path that points at the moved folders.
- R-H2. The layer projects `Domain.Shared`, `Application.Contracts`, `HttpApi`, `HttpApi.Client` are optional and exist only where there is something for them; `fix --rules=DA-A02` creates a layer together with the files that move into it (namespaces and in-project paths unchanged, references, packages and Dockerfile restore lines wired), lists what cannot move with the blocking file, moves controllers together or not at all, and creates an empty layer only with `--empty`. `doctor` DA-A07 flags a layer project without sources.
- R-H3. `doctor` has a built-in opt-in rule set `abp` (`DA-A01..DA-A11`), enabled by `standards: [abp]`; a profile can require it and can list accepted exceptions with a reason.
- R-H4. `new solution --layout=v3` generates the ABP-shaped tree (optionally with the layer projects) and passes the `abp` rule set.
- R-H6. `fix --rules=DA-A08` generates the typed client project from the controllers (only contract types; unsupported actions listed as manual); `doctor` DA-A08 reports a service with controllers and no client.
- R-H8. `doctor` DA-A10 flags loose files at a project root and folders crowded with several features; `fix --rules=DA-A10` moves them into kind and feature folders inside the project (namespaces unchanged); the typed client is generated into the same tree.
- R-H9. `doctor` DA-A11 checks project references (reachable, direction-respecting, no unused forbidden reference), solution membership and Dockerfile restore lines; `fix --rules=DA-A11` repairs them and lists a used forbidden reference as manual.
- R-H7. `fix --rules=DA-A09` moves root Compose files to `etc/docker/`, re-bases their relative paths and updates mentions; `doctor` DA-A09 reports root Compose files.
- R-H5. The tool has no rule about `Guid` ids, application services or ABP runtime features.
- R-I1. All commands (generators, ops, inspection) are operations of one registry; the CLI (`new crud`) and MCP (`new_crud`) surfaces are derived from it and list the same commands with the same values. A command that changes files returns a plan and writes only with apply.
- R-I2. `add_layer`, `add_tests`, `spec_list|add|check` and `graph` exist as registry operations; `add_layer` creates a layer only when files move into it, an empty layer is CLI-only (`--empty`).
- R-I3. `new solution` defaults to layout v3; in an ABP project every generator is followed by the ABP fixers and an `.net-arch/` refresh, and the generated solution builds without warnings, passes its tests and has no ABP finding.
- R-I4. The generated templates are fixer-friendly: DTOs are pure data, the entity mapping is a separate file, repository ports are async and return no `IQueryable`.
- R-I5. The typed client is generated with its tests (the verb and route of every action, and a parity test that every controller route has a client method) and follows the controller routes: an action added later, also in a partial controller file, reaches the client and its tests; `doctor` DA-A08 reports a client that is missing or out of date.
