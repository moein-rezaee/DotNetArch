[فارسی](./README.fa.md)

<img src="assets/icon.png" width="128" height="128" style="vertical-align: middle;"/>

# DotNetArch

A cross-platform .NET global tool (`dotnet-arch`) that scaffolds opinionated **Clean Architecture microservices**, **independent provider-based kits** and exposes itself as an **MCP server** for agents.

---

## Table of Contents
- [What it generates](#what-it-generates)
- [Requirements and installation](#requirements-and-installation)
- [Quick start](#quick-start)
- [Command reference](#command-reference)
- [Generated microservice (layout v2)](#generated-microservice-layout-v2)
- [Configuration model](#configuration-model)
- [Kits (external services)](#kits-external-services)
- [Docker, Git, CI and private registries](#docker-git-ci-and-private-registries)
- [MCP](#mcp)
- [Tests](#tests)
- [Legacy layout](#legacy-layout)
- [Building from source](#building-from-source)
- [Documentation map](#documentation-map)
- [Contributing, license, contact](#contributing-license-contact)

## What it generates
| You run | You get |
| --- | --- |
| `new solution` | Domain / Application / Infrastructure / Api projects, per-layer DI, central package versions, configuration loading, tests per layer, optional Docker, Git, CI, MCP host, agent rules and specs |
| `new crud`, `new action`, `new event`, `new enum`, `new constant` | a vertical slice per entity (commands, queries, actions, events, DTOs, validators, EF mapping, controller or minimal-API endpoints, tests, MCP tools) |
| `new service` | an application service when it has business logic, or a **kit** when it is an external capability |
| `new kit` / `add kit` | `kits/<Area>`: Abstractions + Core + providers, wired into the solution |
| `mcp serve` | an MCP server (stdio) exposing the generators as tools |

Generated code follows Clean Architecture, ports and adapters, vertical slices with CQRS (MediatR), unit of work and repository, SOLID and Clean Code. It builds warning-free in Release and ships its own tests.

## Requirements and installation
- [.NET SDK](https://dotnet.microsoft.com/download) 8.0+ (the tool targets `net8.0` and `net9.0`), Git; Docker is optional.
```bash
dotnet tool install --global DotNetArch      # or: dotnet tool update --global DotNetArch
dotnet-arch --help
```

## Quick start
```bash
dotnet-arch new solution Shop --database=Postgres --mcp --ci=auto --git-remote=git@github.com:acme/shop.git
cd Shop
dotnet-arch new crud --entity=Product
dotnet-arch new action --entity=Product --action=Archive --method=POST
dotnet-arch new event --entity=Product --name=Created
dotnet-arch new service --logic=false --area=Cache --providers=InMemory,Redis   # an external capability becomes a kit
cp src/Shop.Api/.env.example src/Shop.Api/.env                                  # secrets and run-time values
dotnet run --project src/Shop.Api
```
Missing options are prompted for (with defaults) when a terminal is attached; with piped input, answer by number or text, blank = default.

## Command reference
| Command | Purpose |
| --- | --- |
| `new solution <Name>` | Create a solution. Options: `--output`, `--database=SQLite|SqlServer|Postgres`, `--style=controller|fast`, `--layout=v2|legacy`, `--tfm` (from installed SDK), `--mcp`, `--ci=auto|none|github|gitlab|azure|bitbucket|gitea`, `--git-remote`, `--git-provider`, `--git-host`, `--docker-registry`, `--nuget-source`, `--nuget-source-name`, `--no-docker`, `--no-git`, `--no-tests` |
| `new crud --entity=X` | CRUD slice for an entity (`--no-migration` skips creating the EF migration) |
| `new action --entity=X --action=Name --method=GET|POST|PUT|PATCH|DELETE` | Custom use case |
| `new event --entity=X --name=Name` | Domain event, then add subscribers interactively |
| `new enum [--entity=X] --enum=Name` / `new constant [--entity=X] --constant=Name` | Enum / constants scoped to an entity or common |
| `new service` | Business service, or kit when the service has no business logic. Non-interactive: `--logic=true --name= [--entity=] [--lifetime=]` or `--logic=false --area= [--providers=] [--with-tests]` |
| `new kit --area=Cache [--providers=A,B] [--kit-prefix=P] [--with-tests]` | Generate an independent kit and wire it in |
| `add kit <Area>` / `add mcp` | Wire an existing kit / add the MCP host to the solution |
| `ci add [provider]` / `docker add` / `git setup [--remote=] [--git-provider=] [--git-host=]` | Operations files for an existing solution |
| `exec [--docker|--docker-detach|--docker-stop]` | Run the API locally or in Docker (applies migrations first) |
| `remove migration` | Roll back and remove the last migration |
| `mcp serve` | Start the MCP server over stdio |

Exit codes: `0` success, `1` usage/validation error, `2` missing .NET SDK. All names are validated before they reach the file system or a command line; child processes run without a shell.

## Generated microservice (layout v2)
```text
Shop/
├── src/
│   ├── Shop.Domain            entities (private setters, behaviour), events, enums, constants
│   ├── Shop.Application       ports, Features/<Plural>/{Commands,Queries,Actions,Events,Dtos,Services}/<UseCase>/, validators
│   ├── Shop.Infrastructure    EF Core persistence, repositories, unit of work, configuration loading
│   ├── Shop.Api               controllers (or minimal-API endpoints), composition root
│   └── Shop.Mcp               optional MCP host (tools mirror the use cases)
├── kits/                      independent kits (Abstractions / Core / Providers.*)
├── tests/                     one test project per layer
├── docs/                      specs, roadmap, decisions (English + Persian)
├── AGENTS.md  Directory.Build.props  Directory.Packages.props  global.json
├── docker-compose.yml  CI pipeline  NuGet.config (when private feed)  dotnet-arch.yml
```
Dependencies point inwards (`Api/Mcp -> Infrastructure -> Application -> Domain`). Every layer registers its own services (`AddApplication`, `AddInfrastructure`, `AddApi`/`AddMcpHost`) and references only its own packages. Repositories are async and never expose `IQueryable`.

## Configuration model
- **appsettings.json** - non-sensitive settings (PascalCase, colon keys). **Environment / `.env`** - secrets and run-time values (UPPER_CASE, single underscores; `__` for nesting).
- Loaded by one step, `AddAppConfiguration()`, before any options binding: appsettings, appsettings.{Env}, `.env`, `.env.{env}`, real environment variables, command line (later wins).
- `.env.example` and `appsettings.example.json` are generated and checked by tests (`Category=Configuration`, `scripts/validate-examples.sh`); `ConfigurationContract` lists every secret key.

## Kits (external services)
External capabilities are packaged as kits, named by capability (never by product): `MediaStorage` (Minio, RustFs), `Cache` (InMemory, Redis), `MessageBroker` (RabbitMq) or any new area.
```text
kits/Cache/
├── <Prefix>.Kit.Cache.Abstractions     contracts (ICache), no third-party dependencies
├── <Prefix>.Kit.Cache.Core             options, provider selection, AddCacheKit(configuration)
├── <Prefix>.Kit.Cache.Providers.Redis  thin: own config section, own secrets, AddRedisCacheProvider()
├── <Prefix>.Kit.Cache.Providers.InMemory
├── Directory.Build.props (own version)  README (en/fa)  AGENTS.md  docs/specs  scripts/pack.sh
```
`Providers.* -> Core -> Abstractions`. Application references only the Abstractions; the composition root references Core and the providers. The provider is chosen with `Cache:Provider`. Kits build, pack and publish on their own (`scripts/pack-kits.sh`, CI job when `--nuget-source` is set). Built-in provider implementations are verified against stubs, not against live servers.

## Docker, Git, CI and private registries
- Dockerfile (restore-cached multi-stage, non-root), `.dockerignore`, `docker-compose.yml` matching the database (SQLite volume, PostgreSQL, SQL Server).
- CI pipeline for GitHub Actions, GitLab CI, Azure Pipelines, Bitbucket Pipelines or Gitea Actions, chosen automatically from the git remote (`--ci=auto`) or explicitly; personal/self-hosted servers via `--git-provider`/`--git-host`.
- `--docker-registry` and `--nuget-source` add the private registry/feed to the compose image names, CI login and push jobs, `NuGet.config` and the Dockerfile restore. Credentials are never written to files; CI secrets (`REGISTRY_USER`, `REGISTRY_PASSWORD`, `NUGET_USER`, `NUGET_PASSWORD`, `NUGET_API_KEY`) are referenced by name.

## MCP
- **Tool server**: `dotnet-arch mcp serve` (stdio) exposes `new_solution`, `new_crud`, `new_action`, `new_event`, `new_enum`, `new_constant`, `new_service`, `new_kit`, `add_kit`, `add_mcp`, `ci_add`, `docker_add`, `git_setup`, `list_entities`, `describe_config`. Non-interactive; each result lists created/modified files and the equivalent CLI command. No destructive tools.
```json
{ "mcpServers": { "dotnet-arch": { "command": "dotnet-arch", "args": ["mcp", "serve"] } } }
```
- **Generated MCP host** (`--mcp` / `add mcp`): `src/<App>.Mcp`, streamable HTTP at `/mcp`, bearer token `MCP_AUTH_TOKEN`, tools per entity (`product_list|get|create|update|delete`, one per action) that send the same MediatR requests as the controllers.

## Tests
`dotnet test` in a generated solution runs per-layer tests (domain, handlers/validators with fakes, persistence on in-memory SQLite, API and MCP integration). The tool itself has unit tests for Core, Cli and Mcp plus integration tests (golden generation, MCP protocol) run by `scripts/smoke.sh`.

## Legacy layout
Solutions created before v1.3 (flat `<App>.Core/Application/Infrastructure/API`, no `layout:` key in `dotnet-arch.yml`) keep working with every command. `new solution --layout=legacy` still creates that shape.

#### new action
```bash
dotnet-arch new action --entity=EntityName [--action=ActionName] --method=METHOD [--output=Path]
```
Adds a custom command or query to an existing slice. After choosing the HTTP verb, you're prompted for an optional action name—leaving it blank infers a CRUD-style name from the method. The scaffolder infers command versus query based on the HTTP verb. If the slice is missing, a minimal repository and controller are created.

Behavior
- Exact, case‑insensitive CRUD names (Create, Update, Delete, GetById, GetAll, GetList, Patch) are treated as “standard” and generate full end‑to‑end code (handlers, validators, controller/endpoints, and repository methods).
- Any other name is “non‑standard”: no database logic is generated. With a DB provider configured, you’ll be asked whether to add a matching repository method; signatures are parameterless.
- Non‑standard commands and queries do not take inputs:
  - Controller: `public async Task<IActionResult> MyAction()` sends `new MyActionCommand()` / `new MyActionQuery()`
  - Minimal API: `routes.MapX("/Api/<Entity>/MyAction", async (IMediator m) => …)`
- Standard Update always includes `Id` in the command. Controllers use `command with { Id = id }`.

Validation
- The method/action conflict check only applies to exact keywords (case‑insensitive). Examples:
  - POST + Create → allowed; POST + Update → error; POST + UpdateTelegram → allowed.

#### new event
```bash
dotnet-arch new event --entity=EntityName [--event=EventName] [--output=Path]
```
Creates a domain event for an entity and interactively adds subscribers from other slices.

#### new enum
```bash
dotnet-arch new enum [--entity=EntityName] --enum=EnumName [--output=Path]
```
Creates an enum for an existing entity under `Core/Features/<Entity>/Enums`. If no entity is supplied, the enum is placed in `Core/Common/Enums`.

#### new constant
```bash
dotnet-arch new constant [--entity=EntityName] --constant=ConstantName [--output=Path]
```
Creates a constants class for an existing entity under `Core/Features/<Entity>/Constants`. If no entity is supplied, the class is placed in `Core/Common/Constants`.

#### new service
```bash
dotnet-arch new service [--output=Path]
```
Interactive scaffolding for:
- **Custom services** (per‑feature or common)
- **Cache services** powered by Redis
- **Message broker services** using RabbitMQ
- **HttpRequest service wrapper** for calling external APIs

Interfaces and implementations are placed in the appropriate layer and registered automatically.

#### exec
```bash
dotnet-arch exec [--output=Path] [--docker] [--docker-detach] [--docker-stop]
```
Launches the startup project. Detects entity property changes, creates and applies migrations automatically (skipped in No Database mode), and then runs the API. With `--docker`, builds the image, starts the container, streams logs, and tears everything down safely on exit. With `--docker-detach`, performs the same setup with step-by-step logging but leaves the container running in the background without streaming logs or cleaning up. With `--docker-stop`, safely stops and removes the container and image from a detached run.

#### remove migration
```bash
dotnet-arch remove migration [--output=Path]
```
Rolls the database back one migration and deletes the last migration file without running the application.


## Building from source
```bash
dotnet build DotNetArch.sln
dotnet test DotNetArch.sln --filter "Category!=Integration"
scripts/smoke.sh                       # build, unit + integration tests, generate a solution and build it
dotnet run --project src/DotNetArch.Cli -- new solution Demo
```
Layers: `src/DotNetArch.Core` (implementation), `src/DotNetArch.Cli` (commands, console, the global tool), `src/DotNetArch.Mcp` (MCP server). See [CONTRIBUTING.md](CONTRIBUTING.md).

## Documentation map
`docs/specs/` (requirements, architecture, contracts, acceptance, overview, changelog, `openspec.yaml`, `testspec.yaml`), `docs/decisions/` (decision log), `docs/ROADMAP.md`, `AGENTS.md` (rules for agents). Persian mirrors use `.fa.md`; `scripts/check-docs.sh` verifies the pairs.

## Contributing, license, contact
Read [CONTRIBUTING.md](CONTRIBUTING.md) and `AGENTS.md` first. MIT licensed ([LICENSE](LICENSE)). Maintainer: Moein Rezaee - [GitHub](https://github.com/moein-rezaee/DotNetArch).
