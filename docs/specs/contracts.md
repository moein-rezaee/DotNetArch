# Contracts (CLI, config, MCP)

## CLI (current -> target)
Existing and kept: `new solution`, `new crud`, `new action`, `new event`, `new enum`, `new constant`, `exec`, `remove migration`.
Changed: `new service` (D-08): asks "Does this service contain business logic?"; yes = existing internal flow; no = kit flow.
New: `new kit --area <Name> --providers <A,B> [--kit-prefix P] [--output path] [--with-tests]`,
`add kit <Area>` (wire an existing kit into a solution), `mcp serve`, `ci add [--ci provider]`, `docker add`, `git setup`.

`new solution` options (added): `--layout v2` (default), `--tfm net8.0|net9.0`, `--mcp`, `--ci <auto|github|gitlab|azure|bitbucket|none>`,
`--git-host <url> --git-provider <p>`, `--docker-registry <host>`, `--nuget-source <url>`, `--no-tests`, `--no-docker`, `--no-git`.

All arguments are passed to child processes as argument lists, never through a shell string. Names are validated
against `^[A-Za-z][A-Za-z0-9_.]*$` before use (solution, entity, event, kit area, provider).

## `dotnet-arch.yml` keys (additive; unknown keys preserved)
`solution path startup style port framework database layout ci.provider git.host git.provider docker.registry nuget.source
nuget.sourceName kit.prefix kit.<Area>: <Provider,...> mcp: true|false entity.<Name>: crud|action|both`.

## Configuration contract (generated code)
- Non-sensitive: `appsettings.json` PascalCase, colon sections. Secrets/run-time: environment, UPPER_CASE with single underscores.
- One `AddAppConfiguration()` step: appsettings -> environment (last wins) -> `IConfiguration`, before any options binding.
- `.env.example` = complete placeholder-only key inventory; `appsettings.example.json` = complete sanitized model; CI validates both.
- Kit keys: `<Area>:Provider`, `<Area>:<Provider>:<Option>`; secrets `<PROVIDER>_<NAME>` e.g. `MINIO_ACCESS_KEY`.

## MCP: tool server (`dotnet-arch mcp serve`, stdio)
Tools (JSON in/out, no shell): `new_solution`, `new_crud`, `new_action`, `new_event`, `new_enum`, `new_constant`, `new_service`, `new_kit`,
`add_kit`, `ci_add`, `docker_add`, `git_setup`, `list_entities`, `describe_config`. Every tool returns created/changed file paths and the
exact CLI equivalent. Destructive operations are not exposed.

## MCP: generated host (`<App>.Mcp`)
One tool class per entity under `Tools/<Entity>/`; tool names `<entity>_<verb>`; they send the same MediatR requests as controllers.
Auth by bearer token validated like the API; per-tool authorization policy mirrors the controller policy. Health endpoint `/health`.

## Exit codes and output
0 success; 1 validation/usage; 2 environment (missing SDK/docker); 3 generation failure. Human logs go to stderr in MCP mode, stdout otherwise.
