# Roadmap

Persian mirror: `ROADMAP.fa.md`. Legend: `[ ]` todo, `[x]` done (note commit). Requirement IDs refer to `docs/specs/requirements.md`.

## Resume point
**Current phase:** 0 (baseline docs committed). **Next item:** Phase 1.1. Branch: `claude/exciting-fermi-b41fue`.
Open environment risk: no `dotnet` SDK in the cloud container (verification items stay unticked until it is installed or run elsewhere).

## Phase 0 - Docs, specs, rules, roadmap (R-E1, R-D1..D5)
- [x] 0.1 requirements (+fa), decisions (+fa), architecture trees, contracts, acceptance, overview, changelog
- [x] 0.2 AGENTS.md (spec/architecture/config/tool-code/roadmap/git rules)
- [x] 0.3 this roadmap (+fa)
- [x] 0.4 `openspec.yaml` / `testspec.yaml` (machine-readable spec of CLI + acceptance)
- [ ] 0.5 try to install dotnet SDK 8/9 in the container; record result in this file

## Phase 1 - Tool hardening and restructure (R-E4, D-10, D-13)
- [ ] 1.1 Validate all identifiers (solution, entity, event, area, provider)
- [ ] 1.2 Replace `bash -c`/`cmd /c` string runner with `ProcessRunner` (argument lists, env map, cross-platform)
- [ ] 1.3 Fix Windows `ASPNETCORE_ENVIRONMENT=...` prefix via process environment
- [ ] 1.4 Split `Main.cs` into `Commands/*`, `Config/*`, `Infrastructure/*` (behaviour-preserving)
- [ ] 1.5 Shared `MigrationRunner` (dedupe Crud/Action scaffolders)
- [ ] 1.6 Remove `.DS_Store`; extend `.gitignore`
- [ ] 1.7 `scripts/smoke.sh` (+ps1)

## Phase 2 - Microservice core template v2 (R-A1..A8, D-10, D-12)
- [ ] 2.1 `layout: v2` config + legacy detection
- [ ] 2.2 `src/`+`tests/` skeleton, `Directory.Build.props`, `Directory.Packages.props`, `global.json`
- [ ] 2.3 Domain template (private setters, behaviours)
- [ ] 2.4 Application template: Abstractions/ports, Common behaviours, MediatR, FluentValidation, `AddApplication()`
- [ ] 2.5 Infrastructure template: EF Core persistence, async repository, UoW, `AddInfrastructure()`
- [ ] 2.6 Api template: split `Program.cs`, controllers per entity folder (and minimal-API variant)
- [ ] 2.7 `new crud/action/event/enum/constant` emit into `Features/<Entity>/{Commands,Queries,Actions,Events,Dtos}` nested per use case

## Phase 3 - Configuration (R-A9, A10, D-09)
- [ ] 3.1 `AddAppConfiguration()` (appsettings + env in one step)
- [ ] 3.2 Options classes + validation per layer
- [ ] 3.3 Generate `.env.example`, `appsettings.example.json`; `validate-examples` script

## Phase 4 - Docker, Git, CI, registries (R-A11, A12, A15, D-06, D-07)
- [ ] 4.1 Dockerfile + compose + `.dockerignore`
- [ ] 4.2 Git setup incl. personal host/provider
- [ ] 4.3 CI provider detector + GitHub Actions, GitLab CI, Azure, Bitbucket templates
- [ ] 4.4 Personal Docker registry + NuGet source (`NuGet.config`, CI vars, compose image)
- [ ] 4.5 `ci add`, `docker add`, `git setup` commands

## Phase 5 - Tests in generated projects (R-A14, D-02)
- [ ] 5.1 Per-layer test projects with fakes/builders
- [ ] 5.2 CI test job; `testspec.yaml` generation

## Phase 6 - Kits (R-B1..B8, D-05, D-08, D-11, D-14)
- [ ] 6.1 Kit scaffolder (Abstractions, Core, Providers) with docs/specs/AGENTS per kit
- [ ] 6.2 Built-in kit recipes: MediaStorage (Minio, RustFs), Cache (Redis, InMemory), MessageBroker (RabbitMq)
- [ ] 6.3 `new service`: ask business-logic question; internal unchanged, external -> kit
- [ ] 6.4 `add kit` wiring (Abstractions->Application, Core+Providers->composition root, config keys, examples)
- [ ] 6.5 Kit CI jobs + pack/push to personal NuGet registry, per-kit versioning

## Phase 7 - MCP (R-A13, R-C1, D-01)
- [ ] 7.1 `mcp serve` stdio server + tools (contracts.md)
- [ ] 7.2 `<App>.Mcp` host scaffolding: tools per entity via MediatR, auth, health, Dockerfile, tests

## Phase 8 - Documentation completion (R-D1..D5)
- [ ] 8.1 README (+fa) rewrite for new features; command reference
- [ ] 8.2 Generated-project docs/specs/AGENTS templates; kit docs templates
- [ ] 8.3 `.fa.md` mirrors for architecture, contracts, acceptance, overview, changelog
- [ ] 8.4 bilingual-pair check script

## Phase 9 - Verification and release
- [ ] 9.1 Run smoke script end to end (needs SDK); fix findings
- [ ] 9.2 Version bump, changelog, final review
