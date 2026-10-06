# Contributing

Start with `AGENTS.md` (rules), `docs/ROADMAP.md` (what is next) and `docs/decisions/decisions.md` (why things are the way they are).

## Layout
- `src/DotNetArch.Core` - implementation (scaffolding, config, host abstractions). No `Console`, no shell.
- `src/DotNetArch.Cli` - command layer and the global tool `dotnet-arch`.
- `src/DotNetArch.Mcp` - MCP server layer (planned, roadmap phase 7).
- `tests/` - one test project per layer that needs one.
- `samples/` - read-only references; never edited or compiled.

## Workflow
```bash
dotnet build DotNetArch.sln
dotnet test DotNetArch.sln --filter "Category!=Integration"   # fast
scripts/smoke.sh                                              # generates a solution and builds it (needs SDK + NuGet)
```
Behaviour changes update the specs in the same commit; keep `.md` / `.fa.md` pairs in sync.
