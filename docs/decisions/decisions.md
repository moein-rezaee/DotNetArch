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

## D-25 ABP is the structural reference standard (Owner)
The tool follows the structure and rules of the ABP Framework solution and module conventions (spec: `abp-alignment.md`). The ABP runtime framework (modules, multi-tenancy, audit, permissions, localization, settings) is out of scope and stays in Kits. Where a service deliberately differs (CQRS handlers instead of application services, business ids instead of `Guid`), the built-in rule set simply has no rule about it.

## D-26 Layout v3: src / test / etc (Owner)
Layout v3 uses `src/`, `test/` (singular) and `etc/` (docker, scripts, files a service publishes). v2 stays valid for adopted services and is moved by `fix --rules=DA-A01`; legacy flat layouts move with DA-S06 first.

## D-27 Layer projects: optional for the tool, mandatory for a profile (Owner)
`Domain.Shared`, `Application.Contracts`, `HttpApi`, `HttpApi.Client` are not required by the tool. A profile may require them (Corevia does). A layer exists only where there is something for it. `fix --rules=DA-A02` creates a layer project together with the files that move into it (namespaces unchanged, so no code is edited, D-21); it creates nothing for a layer that has nothing to receive. Empty layers are a CLI-only choice (`--empty`), because a human on the CLI owns a development plan for them; an agent over MCP either migrates what belongs to the layer or does nothing. `doctor` DA-A07 flags a layer project without sources.

## D-28 Built-in opt-in `abp` rule set (Proposed)
`doctor` ships a general, public rule set `DA-A01..DA-A09` (folders, layer projects, reference direction, repository shape, naming suffixes, DTO location). It is enabled by `standards: [abp]` in `project.yml`, is off by default and carries no organisation name.

## D-29 A service owns what it publishes (Owner)
Files a service publishes for other systems (such as its gateway route declaration) live inside the service, under `etc/`; consumers aggregate them and the service never depends on a consumer. Which file and which consumer is organisation knowledge and belongs to a profile, not to the tool.

## D-30 A complete migration puts everything in its standard place, including the typed client (Owner)
Migrating to the standard means every file ends in its standard location with its references fixed: files that belong in a layer move there, root Compose files go to `etc/docker/`, and a service with controllers gets its typed client (`HttpApi.Client`, DA-A08) generated from the controllers. A layer that would stay empty does not exist; a layer with a purpose is filled in the same migration. The API request and response models are input and output contracts and live in Application.Contracts, so the client can use them without referencing ASP.NET Core.
