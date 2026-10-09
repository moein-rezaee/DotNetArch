# Contracts (CLI, config, MCP)

## CLI
Commands (all registry operations, D-32; a two-word command is the operation with `_`: `new crud` = `new_crud`): `new solution`, `new crud`, `new action`, `new event`, `new enum`, `new constant`, `new service`, `new kit`, `add kit`, `add mcp`, `add layer`, `add tests`,
`ci add`, `docker add`, `git setup`, `spec list`, `spec add`, `spec check`, `graph`, `list entities`, `describe config`, `exec`, `remove migration`, `doctor`, `adopt`, `fix`, `mcp serve`.
Every command that changes files plans first and writes only with `--apply`; at a terminal the plan is shown and the user is asked. `--json` prints `{ ok, operation, applied, error?, plan[], data }`, `--out=file` saves the output. `--path` (alias `--output`) names the solution folder; values may be written `--no-migration` or `--no_migration`. Missing required values are prompted for.

`new solution <Name>` options: `--output`, `--database=SQLite|SqlServer|Postgres`, `--style=controller|fast`, `--layout=v3|v2|legacy` (default v3),
`--mcp`, `--ci=auto|none|github|gitlab|azure|bitbucket|gitea`, `--git-remote`, `--git-provider`, `--git-host`, `--docker-registry`, `--nuget-source`,
`--nuget-source-name`, `--no-docker`, `--no-git`, `--no-tests`. The target framework follows the highest installed SDK (8 or 9).

`new service`: with business logic (`--logic=true --name= [--entity=] [--lifetime=Scoped|Transient|Singleton]`) generates an Application service registered in
`AddApplication()`; without (`--logic=false --area= [--providers=A,B] [--with-tests]`) generates a kit and wires it. Interactive when `--logic` is absent.

`new kit --area=<Area> [--providers=A,B] [--kit-prefix=P] [--with-tests] [--no-wire]`; `add kit <Area>`. Built-in areas: MediaStorage (Minio, RustFs), Cache (InMemory, Redis),
MessageBroker (RabbitMq); any other area name gets a generic skeleton. `new crud` / `new action` accept `--no-migration`.

All arguments reach child processes as argument lists (no shell). Names are validated (`^[A-Za-z][A-Za-z0-9_]*$`, dotted segments allowed for solution names and kit prefixes)
before use. Interactive questions can be answered from piped input by option number or text; a blank line takes the default.

## dotnet-arch.yml keys
`solution path startup style port framework database layout ci.provider git.provider git.host docker.image docker.container docker.registry nuget.source nuget.sourceName
kit.prefix kit.<Area>: <Providers,...> mcp: true entity.<Name>: crud|action|both`. A missing `layout` key means the legacy layout.

## Configuration contract (generated code)
- Non-sensitive: `appsettings.json` (PascalCase, colon sections). Secrets and run-time values: environment or `.env` (UPPER_CASE, single underscores, `__` for nesting).
- `AddAppConfiguration()` (Infrastructure, shared by Api and Mcp) loads appsettings, appsettings.{Env}, `.env`, `.env.{env}`, environment, command line (later wins) before any options binding.
- `.env.example` is the complete placeholder-only inventory of secrets; `appsettings.example.json` mirrors `appsettings.json`; `ConfigurationContract.SecretKeys` lists the secrets; tests tagged `Category=Configuration` enforce all three.
- Kit keys: `<Area>:Provider`, `<Area>:<Provider>:<Option>`; secrets `<PROVIDER>_<NAME>` (for example `REDIS_PASSWORD`, `MINIO_ACCESS_KEY`).

## MCP: tool server
`dotnet-arch mcp serve` (stdio). Every operation of the registry is a tool with the same name, description and values as the CLI command (`new_solution`, `new_crud`, `new_action`, `new_event`, `new_enum`, `new_constant`, `new_service`, `new_kit`, `add_kit`, `add_mcp`, `add_layer`, `add_tests`, `ci_add`, `docker_add`, `git_setup`, `spec_list`, `spec_add`, `spec_check`, `graph`, `list_entities`, `describe_config`, `exec`, `remove_migration`, `doctor`, `adopt`, `fix`). Non-interactive, no shell. Read-only tools are marked read-only; every mutating tool has an `apply` flag (default false: the plan only) and returns `{ ok, operation, applied, error?, plan[], data }`. Parameters marked CLI-only (for example `empty` of `add_layer`) are not offered. No tool is destructive; `remove_migration` and `exec` act only with `apply: true`.

## MCP: generated host
`src/<App>.Mcp`, streamable HTTP at `/mcp` (stateless), `/health` open. Authentication: bearer token compared in constant time with `MCP_AUTH_TOKEN` (secret, at least 24 characters).
Tools per entity: `<entity>_list|get|create|update|delete`; per action: `<entity>_<action>`. Each tool sends the same MediatR request as the controller or endpoint; expected failures
(validation, not found, business rule) become readable MCP errors.

## Exit codes and output
`0` success, `1` usage or validation error, `2` .NET SDK missing. Human logs go to stdout in the CLI and to stderr in `mcp serve`.

## Doctor
`dotnet-arch doctor [path] [--profile=auto|generic|corevia] [--json] [--strict] [--out=file]` and MCP tool `doctor(repositoryPath, profile, json)`. Read-only: no file is written (except `--out`), no process is started, no .NET SDK is required.
Finding: `{ id: "DA-<area><nn>", severity: error|warning|info, category, message, path?, hint?, details?[] }`. Ids: S structure, B build, C config, D container/CI, M docs, K code, V corevia profile.
Exit codes: `0` healthy, `1` usage error, `3` blocking findings.

## Operations registry, adopt, fix, .net-arch
`doctor`, `adopt` and `fix` are defined once in the operation registry; the CLI (`dotnet-arch <op> [path] [--json] [--apply] [--out=file]`) and the MCP tools (`doctor`, `adopt`, `fix`) derive from it. `--json` / the MCP result is `{ ok, operation, applied, error?, plan[], data }`; a plan item is `{ path, action: create|modify, reason, ruleId? }`. Mutating operations write only with `--apply` (MCP `apply: true`).
`adopt [path] [--profile=file]` writes only `.net-arch/project.yml` (schema, blueprint, layout, mcp, layers, kits, entities, modules), `rules.yml` (once), `generated.lock` (once) and, with `--profile`, `profile.yml` (once, `source` relative to the repository root). `fix [path] [--rules=ids]` fixes by default DA-B01 (global.json), DA-B05 (a rule-less .editorconfig marker, so formatting checks keep passing on existing code), DA-B08/B09 (.gitignore) and DA-D04 (.dockerignore) and lists everything else as manual. Opt-in structural rules, named in `--rules`: DA-B03 (central package versions; differing pins stay as `VersionOverride`, resolved versions unchanged), DA-B07 (warnings are errors in CI/Release), DA-S04 (Domain test project with an architecture test, registered with `dotnet sln add`), DA-S06 (layout v2: projects to `src/`, test projects to `tests/`; the solution, project references, Dockerfile/compose/CI/.corevia paths, README/spec paths and path literals in code are rewritten; history files such as changelogs and evidence are left alone). A move is a planned change with action `move`.

ABP alignment (layout v3, see `abp-alignment.md`): `project.yml` gets `standards: [abp]` (built-in opt-in rule set DA-A01..DA-A11; `profile.yml` may also carry `standards` and a `severity` map that raises or lowers any rule id, below rules.yml in precedence) and `layout: v3` (`src/`, `test/`, optional `etc/`). `fix` adds opt-in rules DA-A01 (v2 to v3: `tests/` to `test/`, same path rewriting as DA-S06) and DA-A02 (content-driven layers: create `Domain.Shared`, `Application.Contracts` and `HttpApi` only where files move into them - enum-only files, DTOs and MediatR requests, controllers - keeping namespaces and in-project paths, wiring references, packages and the Dockerfile restore stage; `--empty` also creates empty layers incl. `HttpApi.Client`; what cannot move is listed as manual). DA-A08 generates the typed client project (`HttpApi.Client`) from the controllers (new code, contract types only; unsupported actions are manual) and DA-A09 moves root Compose files to `etc/docker/` with re-based paths. `new solution --layout=v3` generates the v3 tree; the layers it generates are the ones that have content.
`rules.yml`: `severity` (id to off|info|warning|error), `thresholds` (`coverage_line`), `exceptions` (`rule`, mandatory `reason`, optional `path`). `profile.yml`: `name`, `version`, optional `source`, `accepted_layouts`, `rules[]` with kinds `require-files`, `forbid-project-reference`, `dockerfile-forbid-line`, `folder-prefix`, `note-if-files-match`.
Ids added to the doctor: DA-M06..M09 (documentation), DA-T01..T02 (tests and coverage).

Typed client transport (`profile.yml`): by default the generated client takes an `HttpClient`. A profile may name a string-returning REST client abstraction instead: `client: { transport: rest-client, interface: <name>, namespace: <namespace>, package: <NuGet package> }`. The abstraction must have the shape `Task<string> GetAsync(string path, IDictionary<string,string>? headers, IDictionary<string,string?>? query, CancellationToken ct)`, `PostAsync/PutAsync/PatchAsync(string path, object? body, headers, query, ct)` and `DeleteAsync(path, headers, query, ct)`; the generated client then takes it in its constructor, deserialises the returned JSON with `System.Text.Json` (web defaults), puts the query string into the path so repeated keys survive, and the client project references the named package. Raw-byte responses cannot be expressed with it and are listed as manual. The tool names no organisation or product; the profile supplies the names.
