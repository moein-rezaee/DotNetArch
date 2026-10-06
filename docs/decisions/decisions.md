# Decision log

Format: ID, decision, reason, consequences. Owner-made decisions are marked **Owner**; decisions I made
under the owner's rules are marked **Proposed** and stay open to change. Persian mirror: `decisions.fa.md`.

## D-01 Two distinct MCP modes (Owner)
(a) `dotnet-arch mcp serve` (stdio): MCP server for using the tool/library itself. (b) Generated-project MCP
host (`<App>.Mcp`): tools mirror the project's controllers/services and dispatch through the same MediatR
commands/queries. Reason: R-C1 and R-A13 are different audiences. Consequence: two separate roadmap phases.

## D-02 Tests only for generated projects (Owner)
The DotNetArch tool repo has no unit-test project. Generated projects get a test project per layer. Kits get none by
default (opt-in `--with-tests`). Consequence/risk: refactors of the tool are verified by a smoke script
(`scripts/smoke.sh`: generate a solution, build it, run its tests), not unit tests. Needs dotnet SDK in the environment.

## D-03 Reference microservice is `samples/corevia-identity`, reference kit is `samples/MediaStorage` (Owner)
Samples are read-only references (see D-10).

## D-04 MediatR for CQRS (Owner, "like the sample")
Use MediatR. Pin to 12.x (Apache-2.0); later majors are commercially licensed. The version is a single
property in `Directory.Packages.props` so it can be changed in one place.

## D-05 Kits: one folder, grouped by business area (Owner)
`kits/<Area>/<Prefix>.Kit.<Area>.{Abstractions,Core,Providers.<Name>}`. Folder name `kits` (the owner said
"extension or kit"). Kits are independent: own `Directory.Build.props`, own version, own CI job, own docs/specs.

## D-06 CI auto-selection (Owner)
Detect from `git remote get-url origin` host and repo markers: github.com -> GitHub Actions; gitlab.* ->
GitLab CI; dev.azure.com -> Azure Pipelines; bitbucket -> Bitbucket Pipelines; unknown host -> ask once and store in
`dotnet-arch.yml` (`ci.provider`, `git.host`). `--ci <provider|none>` overrides. Personal/self-hosted git:
`--git-host <url> --git-provider <github|gitlab|gitea|azure|bitbucket>`.

## D-07 Personal registries (Owner)
`--docker-registry <host>` and `--nuget-source <url>` (+ `--nuget-source-name`). Stored in `dotnet-arch.yml`; written to
`NuGet.config`, CI variables, `docker-compose.yml` image names and Kit publish jobs. Credentials are never written to
files: only CI variable names / env keys.

## D-08 `new service` replaced by internal / kit modes (Owner)
Internal mode unchanged. External mode generates a Kit (R-B3). Prompt wording: "Does this service contain
business logic?".

## D-09 Configuration model (Owner)
Env = secrets + run-time values (UPPER_CASE keys); appsettings = non-sensitive (PascalCase, colon). Loaded in one
`AddAppConfiguration()` step before any options binding. Examples are mandatory (`.env.example`,
`appsettings.example.json`) and validated by a script in CI.

## D-10 Deviations from the reference sample (Owner rule + review findings)
Best practice per owner's description wins over the sample. Defects found in `corevia-identity` that generated code
must NOT copy: sync-over-async (`GetAwaiter().GetResult()` in DI and seed service); sync `IQueryable.Any()` in handlers
(use async repository methods/specifications); leaking `IQueryable` from `IRepository`; anemic domain with public
setters (use private setters and behaviour methods for generated aggregates); non-domain ports in `Domain`
(`IOtpClient`, `IDbInitializer` belong to Application/Infrastructure); implementations in Application (`JwtService`,
seed service belong in Infrastructure, port stays in Application); inconsistent feature subfolders (`Models/Options/Services`
vs `Dtos/Validators`); flat controllers folder (group per entity); EF Design package referenced from Api; 250-line
`Program.cs` (split into extension methods per concern); `Console.WriteLine` stack traces and `AllowAnyOrigin` default CORS.

## D-11 Generated code has no private Corevia dependency (Proposed)
The sample uses private `Corevia.Kit.*` packages (ErrorHandling, ConfigLoader, ...). Generated solutions and kits
must build with public NuGet only (plus the user's own registry). Each kit ships its own small exception types.

## D-12 New default layout with versioned template (Proposed)
New solutions use `src/` + `tests/` layout and `layout: v2` in `dotnet-arch.yml`. Absence of the key means legacy
layout; legacy commands (`new crud` etc.) keep working on legacy solutions.

## D-13 Single tool package (Proposed)
One global tool `dotnet-arch`, MCP server is a sub-command (`dotnet-arch mcp serve`), not a second package.
Source stays one project, organised by folders (Commands, Config, Infrastructure, Mcp, Scaffolding/<Area>, Templates).

## D-14 Kit naming prefix (Proposed)
`<Prefix>` = `--kit-prefix` or the solution/organisation name from `dotnet-arch.yml`. Package ids
`<Prefix>.Kit.<Area>.<Part>`. The tool itself ships no hard-coded company prefix.

## D-15 Target frameworks (Proposed)
Generated code targets net8.0 or net9.0 as selected; `global.json` pinned with `rollForward: latestFeature`.

## D-16 Process (Owner)
Docs/specs/rules/roadmap first, then phases; commit + push per phase; roadmap checkboxes are the resume state.
