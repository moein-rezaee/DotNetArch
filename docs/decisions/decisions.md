# Decision log

Format: ID, decision, reason, consequences. Owner-made decisions are marked **Owner**; decisions I made
under the owner's rules are marked **Proposed** and stay open to change. Persian mirror: `decisions.fa.md`.

## D-01 Two distinct MCP modes (Owner)
(a) `dotnet-arch mcp serve` (stdio): MCP server for using the tool/library itself. (b) Generated-project MCP
host (`<App>.Mcp`): tools mirror the project's controllers/services and dispatch through the same MediatR
commands/queries. Reason: R-C1 and R-A13 are different audiences. Consequence: two separate roadmap phases.

## D-02 Test layer only where needed (Owner, revised by D-17)
Generated projects get a test project per layer. Kits get none by default (opt-in `--with-tests`). The tool itself gets a test
project only for the layers that need one (see D-17). `scripts/smoke.sh` stays as the end-to-end check (generate, build, test).

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

## D-13 One installable tool package (Proposed; layering superseded by D-17)
One global tool `dotnet-arch` (package id `DotNetArch`); MCP server is a sub-command (`dotnet-arch mcp serve`), not a second package.

## D-14 Kit naming prefix (Proposed)
`<Prefix>` = `--kit-prefix` or the solution/organisation name from `dotnet-arch.yml`. Package ids
`<Prefix>.Kit.<Area>.<Part>`. The tool itself ships no hard-coded company prefix.

## D-15 Target frameworks (Proposed)
Generated code targets net8.0 or net9.0 as selected; `global.json` pinned with `rollForward: latestFeature`.

## D-16 Process (Owner)
Docs/specs/rules/roadmap first, then phases; commit + push per phase; roadmap checkboxes are the resume state.

## D-17 Standard multi-layer tool structure (Owner)
The tool is split into layers, each its own project under `src/`: **Cli** (command layer: argument parsing, interactive prompts,
console output, packaged as the global tool), **Mcp** (MCP server layer: tools that call Core with a non-interactive host), **Core**
(all implementation: config, scaffolding, templates, process/file abstractions). Dependency rule: `Cli -> Core`, `Mcp -> Core`,
`Cli -> Mcp` (only to host `mcp serve`); Core references neither. A test project exists per layer that needs one under `tests/`
(`Core.Tests` golden-tree/validation/config tests, `Cli.Tests` argument parsing, `Mcp.Tests` tool catalogue). Replaces the
single-project layout of D-13.

## D-18 Host abstraction for Core (Proposed)
Core never touches `Console` or spawns shells directly. It uses `ToolHost` (ambient, swappable per run) exposing `IPrompter`,
`IProcessRunner`, `IToolOutput`. Cli installs console implementations; Mcp installs a non-interactive prompter (defaults or explicit
"missing option" error) and a stderr/JSON output; tests install fakes. Scaffolders stay static for now; migrating them to injected
instances is a later, tracked item (roadmap 1.9).

## D-19 The project never depends on the tool (Owner)
Everything a generated or adopted project needs (code, tests, documentation, specs, OpenAPI snapshot, CI, Docker, its own MCP host) is ordinary files in
the repository. The tool keeps only `.net-arch/` (state, rules, profile reference, generated.lock). Deleting `.net-arch/` breaks nothing; adopting a project
changes no source file. The state is a cache that `adopt` can rebuild from the source. Consequence: a test proves it (adopt, delete, doctor/build unchanged).

## D-20 One operation registry for CLI and MCP (Proposed)
Each operation (`doctor`, `adopt`, `fix`, later the generators) is defined once with name, description, parameters and read-only/mutating kind. The CLI maps
arguments onto it and the MCP server derives its tools from it, so the two cannot drift. Mutating operations return a plan (`--json`) and write only with
`--apply` (MCP: `apply: true`). The existing generator tools move onto the registry in later cycles.

## D-21 The tool changes structure, never business code (Owner)
`fix` creates missing hygiene files from the tool's own templates and appends missing ignore lines; it never edits source code. Findings that need a code
change are listed as manual and can be recorded as exceptions with a reason in `rules.yml`.

## D-22 No private part in the tool; organisations bring a profile (Owner)
Anything is either general and part of DotNetArch, or it is not added. Organisation rules (required files, forbidden references, folder naming, ...) are a
declarative profile (`profile.yml`, or a shared file referenced by `source`) with a fixed set of rule kinds. The tool never names an organisation.

## D-23 Settings layers (Proposed)
Tool defaults < global `~/.net-arch/` (override with `DOTNET_ARCH_HOME`) < project `.net-arch/`. The project stores only differences. A legacy
`dotnet-arch.yml` keeps working for the generators.

## D-24 Mission (Owner)
Build, adopt, measure, deploy and upgrade Clean Architecture microservices, with identical CLI and MCP. Deployment, CI, graph and documentation modules are
optional and arrive as separate phases; kit grouping is out of scope for now.
