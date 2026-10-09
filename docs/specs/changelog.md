# Changelog

## Unreleased
- New `dotnet-arch doctor` (read-only diagnosis: layers, dependency direction, tests and coverage, build hygiene, configuration and secrets, Docker/CI, documentation, code rules, kit boundaries; text or `--json`; exit code 3 when blocking).
- New `dotnet-arch adopt` (brings an existing project under control by writing only `.net-arch/`) and `dotnet-arch fix` (mechanical hygiene fixes, plan first, `--apply` to write).
- New `.net-arch/` folder (`project.yml`, `rules.yml`, `profile.yml`, `generated.lock`) with a global `~/.net-arch/`; rules.yml carries severity overrides, thresholds and exceptions with a reason.
- Organisation rules are a declarative profile (`profile.yml`); the tool itself contains no organisation-specific check.
- One operation registry feeds the CLI and the MCP server (3 registry tools: `doctor`, `adopt`, `fix`; 18 tools in total).
- `fix` opt-in structural rules: DA-B03 central package versions, DA-B07 warnings as errors, DA-S04 Domain test project, DA-S06 layout v2 move (projects to `src/`, tests to `tests/`, every pointing path rewritten); the default `.editorconfig` fix is now a rule-less marker so lint keeps passing on existing code.
- Doctor: documentation validation (index, specs, OpenAPI snapshot) and test checks (tests present, coverage versus threshold).
- ABP alignment accepted (D-25..D-29, `abp-alignment.md`): layout v3 `src/test/etc`, optional layer projects, opt-in `abp` rule set in `doctor`, `fix` rules DA-A01/DA-A02, `new solution --layout=v3` (implementation tracked in roadmap Phase 12).

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
