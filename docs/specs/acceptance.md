# Acceptance criteria

Verification: per-layer tests under `tests/` (D-17) plus `scripts/smoke.sh`, which generates into a temp dir, then builds and tests the output.
Nothing is claimed verified unless the script ran; when the SDK is unavailable the item stays unticked in the roadmap.

- AC-1 (R-A1..A8) Generated v2 solution builds; project references match the dependency rule in `architecture.md`; Application has no reference to Infrastructure or Kit Core/Providers; each layer csproj lists only its needed packages.
- AC-2 (R-A4) `new crud Product` creates `Features/Products/{Commands,Queries,Actions?,Dtos}/<Name>/...`; `new action` adds under `Actions/<Name>/` of the same entity folder.
- AC-3 (R-A9,A10) Setting a secret only in env and a non-sensitive value only in appsettings both appear in `IConfiguration`; examples contain every key used by options; validate script passes.
- AC-4 (R-A11,A12) `docker compose build` works for the Api; git repo initialised with initial commit unless `--no-git`.
- AC-5 (R-A13) Generated `<App>.Mcp` lists one tool per controller action, calls MediatR, and a unit test proves tool == controller result.
- AC-6 (R-A14) Each layer has a test project; `dotnet test` passes on a freshly generated solution. The tool repo has tests only for layers that need them (D-17).
- AC-7 (R-A15) For remotes github.com / gitlab.* / azure / bitbucket the matching CI file is produced; unknown host stores a choice; `--docker-registry` and `--nuget-source` appear in NuGet.config, CI and compose without credentials.
- AC-8 (R-B1,B2) `new service` internal mode output unchanged from v1.2.0; external mode produces a kit, not a service.
- AC-9 (R-B3..B8) `new kit --area Cache --providers Redis,InMemory` produces `kits/Cache/{Abstractions,Core,Providers.Redis,Providers.InMemory}`, builds standalone, packs with distinct versions, and `add kit Cache` wires Abstractions into Application and Core+Providers into the composition root only.
- AC-10 (R-C1) An MCP client can list tools from `dotnet-arch mcp serve` and create a solution and an entity; results list file paths.
- AC-11 (R-D1..D5) Tool repo, generated project and generated kit each contain AGENTS.md, specs, README pair; the bilingual-pair check script passes.
- AC-12 (D-10) Generated code contains none of the listed sample defects (grep-based check in smoke script: no `GetAwaiter().GetResult()`, no `IQueryable` in `IRepository`, no `AllowAnyOrigin` default, no stack traces via Console).
- AC-13 (D-11) Generated output restores from public NuGet only.
