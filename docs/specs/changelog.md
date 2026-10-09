# Changelog

## 2.0.0 (unreleased)
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
