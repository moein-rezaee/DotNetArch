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
- R-F2. Profiles: `generic` (public standard, layout v2) and `corevia` (adds governance rules, accepts the flat root layout); `auto` picks `corevia` when `.corevia/` exists.
- R-F3. Output for humans (text) and agents (`--json`, MCP tool `doctor`); findings carry id, severity, locations and a fix hint. Exit code `3` when blocking (errors, or warnings with `--strict`).
