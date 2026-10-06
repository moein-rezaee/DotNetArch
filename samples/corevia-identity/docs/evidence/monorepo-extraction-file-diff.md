# Monorepo Extraction File-List Diff (Phase C item 2)

- Date: 2026-10-04
- Source: `corevia-market/IdentityService/` @ corevia-market `develop` 63cb7d92 (snapshot, no history)
- Destination: this repository, flat layout (`target_path: .`) per `corevia-bridge/values/service-repos/corevia-identity.extract.yaml`
- Executed manually (not via `corevia-run apply`) because the executor's apply would also change the origin remote, run `ensure-gitlab-project`, and commit/push to `main`; this pilot is limited to feature branches merged into `develop`. The executor's own rules were replicated: no overwrite of existing target files, excludes `.git .env bin obj .vs .idea .DS_Store`.

## Counts

| Set | Files |
| --- | --- |
| Source files (excluding bin/obj/.DS_Store) | 274 |
| Copied | 272 |
| Excluded on purpose | 2 |
| Existing target files kept (not overwritten) | 1 (`AGENTS.md`) |

## Source files NOT copied

| File | Reason |
| --- | --- |
| `IdentityService.Api/.env` | Real env file; secrets never copied. It is tracked in the monorepo git index (follow-up for the monorepo owner); `.env.example` and `appsettings.example.json` were copied and exist here. |
| `.ci-rebuild-trigger` | Monorepo CI trigger marker; not part of the service. |

## Source file kept un-overwritten

| File | Reason |
| --- | --- |
| `AGENTS.md` | Destination already has the bootstrap `AGENTS.md`. The long service guide stays in the (frozen) monorepo copy until Phase C item 8 (agent-context separation) merges it. |

## Destination-only files (bootstrap, pre-existing)

`.corevia/{follows,repo,skills,validators}.yaml`, `.corevia/mcp/identity.values.yaml`, `.corevia/operations/repo-bootstrap.values.yaml`, `.gitignore`, `docs/roadmaps/roadmap.md`, `docs/specs/openspec.yaml`, `docs/specs/testspec.yaml`.

## Added by hand (needed to build outside the monorepo; not in IdentityService/)

| File | Origin |
| --- | --- |
| `IdentityService.sln` | New solution containing all 7 projects (the monorepo only had the root `Corevia.sln`). |
| `global.json`, `NuGet.config`, `Directory.Build.targets` | Verbatim copies of the corevia-market root files (SDK pin, Nexus feed without credentials, `.env` copy-to-output rule). |

## Preserved

`IdentityService.Infrastructure.Database` (single DB project, corevia-market develop @ 4fc813e3) is present; there are no `Infrastructure.Postgres`/`Infrastructure.SqlServer` projects.

## Intentionally changed during Kit migration (dropped or renamed files)

Complete list of baseline files (corevia-market `develop` 63cb7d92, `IdentityService/`) that are not present under the same path in this repository. Full-diff check: `git ls-tree` of the baseline versus `git ls-files` here shows exactly three such paths; every other baseline file exists at the same path (some with content changes listed in the next section).

| Baseline file | Result here | Reason |
| --- | --- | --- |
| `IdentityService.Infrastructure/Otp/OtpHttpClient.cs` | Renamed to `IdentityService.Infrastructure/Otp/OtpRestClient.cs` (git rename R062); tests in `IdentityService.Infrastructure.Tests/Otp/OtpRestClientTests.cs` | Intentionally changed during Kit migration: the hand-written `HttpClient` wrapper was replaced by `OtpRestClient`, which uses the Kit Http client extension (`Corevia.Kit.HttpClientRestExtension`) per the Kit-First rule. Behavior (OTP send/verify contract) is unchanged. |
| `IdentityService.Api/.env` | Not copied | Real env file with secrets; never copied (see "Source files NOT copied"). `.env.example` is kept. |
| `.ci-rebuild-trigger` | Not copied | Monorepo CI trigger marker; not part of the service. |
| `IdentityService.Infrastructure.Database/DbInitializer.cs` | Moved to `Infrastructure.Database/Initialization/DbInitializer.cs` (commit c488124) | Post-pilot folder-per-concern reorganization: the flat `Infrastructure.Database/` root was split into `Context/`, `Repositories/`, `Providers/`, `Initialization/` subfolders with matching namespaces, to match this codebase's own convention (e.g. `Application/Features/<X>/...`). No behavior change; `dotnet build`/`dotnet test` re-verified 0 errors, 236/236 passing. |
| `IdentityService.Infrastructure.Database/EfRepository.cs` | Moved to `Infrastructure.Database/Repositories/EfRepository.cs` (commit c488124) | Same folder-per-concern reorganization as above. |
| `IdentityService.Infrastructure.Database/IdentityDbContext.cs` | Moved to `Infrastructure.Database/Context/IdentityDbContext.cs` (commit c488124) | Same folder-per-concern reorganization as above. |
| `IdentityService.Infrastructure.Database/IdentityDbContextFactory.cs` | Moved to `Infrastructure.Database/Context/IdentityDbContextFactory.cs` (commit c488124) | Same folder-per-concern reorganization as above. |
| `IdentityService.Infrastructure.Database/UnitOfWork.cs` | Moved to `Infrastructure.Database/Repositories/UnitOfWork.cs` (commit c488124) | Same folder-per-concern reorganization as above. |

## Intentionally changed during Kit migration (modified files, same path)

Files that exist at the same path but differ from the baseline (compared with `git show 63cb7d92:IdentityService/<path>` versus this repository):

| Group | Files | Reason |
| --- | --- | --- |
| Project files and host startup | `IdentityService.Api/IdentityService.Api.csproj`, `IdentityService.Application/IdentityService.Application.csproj`, `IdentityService.Infrastructure/IdentityService.Infrastructure.csproj`, `IdentityService.Infrastructure.Database/IdentityService.Infrastructure.Database.csproj`, `IdentityService.Mcp/IdentityService.Mcp.csproj`, `IdentityService.Api/Program.cs`, `IdentityService.Mcp/Program.cs`, `IdentityService.Api/Dockerfile`, `docker-compose.yml` | `shared/*` ProjectReferences replaced by Kit references (`CoreviaKitPackagesRoot` stopgap, Kit not yet on Nexus); Application references Kit Abstractions only; Docker build context made standalone. |
| Database | `IdentityService.Infrastructure.Database/{DbInitializer,DependencyInjection,IdentityDbContextFactory}.cs`, `IdentityService.Infrastructure/DependencyInjection.cs` | Per-engine projects consolidated into one Kit-based `Infrastructure.Database`; providers registered through Kit DatabaseConnection. |
| Application handlers and options | `IdentityService.Application/Features/**/*Handler.cs` (command/query handlers), `Features/Identity/Options/IdentityClientOptions.cs` | `using ErrorHandling.Core.Exceptions` replaced by `using Corevia.Kit.ErrorHandling.Abstractions.Exceptions` (Kit namespace); no behavior change. |
| Api controllers | `OidcController.cs`, `ProfileController.cs`, `SessionsController.cs` | Same Kit ErrorHandling namespace change; routes unchanged (validator check 8). |
| Mcp | `IdentityService.Mcp/Mcp/McpExecutionContext.cs`, `IdentityService.Mcp/appsettings.json`; added `Mcp/McpTransportSelector.cs` | `Mcp:Transport` default changed from `stdio` to `http` (stdio development-only) per the MCP standard; `McpAuthorizationException` now derives from the Kit `ServiceException`; `McpTransportSelector.cs` is new. |
| Docs and agent context | `AGENTS.md`, `README*.md`, `docs/specs/*`, `docs/integrations/*` | Bilingual standards, scoped agent context and changelog entries. |
