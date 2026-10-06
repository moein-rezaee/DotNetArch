# Roadmap

Persian mirror: `ROADMAP.fa.md`. Legend: `[ ]` todo, `[x]` done (note commit). Requirement IDs refer to `docs/specs/requirements.md`.

## Resume point
**Current phase:** 1 (restructure done; remaining 1.8, 1.9b, 1.10, 1.11b, 1.12). **Next item:** 1.8 then 1.10 (tests). Branch: `claude/exciting-fermi-b41fue`.
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
- [ ] 1.8 Identifier/path validation in Core.Validation: `Identifier` exists and the Cli validates solution names; still to do: enforce inside Core entry points (so MCP is covered), validate output paths, event/enum/constant/action names
- [x] 1.9 Shared `MigrationService` (dedupe Crud/Action/exec/remove; no more `SetCurrentDirectory`)
- [ ] 1.9b Migrate static scaffolders (`CrudScaffolder` ...) to injected instances (D-18 follow-up)
- [ ] 1.10 `DotNetArch.Core.Tests` (validation, config round-trip, golden tree) and `DotNetArch.Cli.Tests` (arg parsing)
- [x] 1.11a `.DS_Store` removed from the repo
- [ ] 1.11b extend `.gitignore` (.DS_Store, rider/vscode), add `scripts/smoke.sh`
- [ ] 1.12 Update README build/run instructions for the new layout

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
