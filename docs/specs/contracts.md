# Contracts (CLI, config, MCP)

## CLI
Commands: `new solution`, `new crud`, `new action`, `new event`, `new enum`, `new constant`, `new service`, `new kit`, `add kit`, `add mcp`,
`ci add`, `docker add`, `git setup`, `exec`, `remove migration`, `mcp serve`.

`new solution <Name>` options: `--output`, `--database=SQLite|SqlServer|Postgres` (layout v2), `--style=controller|fast`, `--layout=v2|legacy` (default v2),
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
`dotnet-arch mcp serve` (stdio). Tools (JSON in/out, non-interactive, no shell): `new_solution`, `new_crud`, `new_action`, `new_event`, `new_enum`, `new_constant`, `new_service`,
`new_kit`, `add_kit`, `add_mcp`, `ci_add`, `docker_add`, `git_setup`, `list_entities`, `describe_config`. Every result is `{ ok, command, output, created[], modified[], error? }`.
`list_entities` and `describe_config` are read-only; no tool deletes anything.

## MCP: generated host
`src/<App>.Mcp`, streamable HTTP at `/mcp` (stateless), `/health` open. Authentication: bearer token compared in constant time with `MCP_AUTH_TOKEN` (secret, at least 24 characters).
Tools per entity: `<entity>_list|get|create|update|delete`; per action: `<entity>_<action>`. Each tool sends the same MediatR request as the controller or endpoint; expected failures
(validation, not found, business rule) become readable MCP errors.

## Exit codes and output
`0` success, `1` usage or validation error, `2` .NET SDK missing. Human logs go to stdout in the CLI and to stderr in `mcp serve`.
