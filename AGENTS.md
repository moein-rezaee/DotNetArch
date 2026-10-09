# AGENTS - DotNetArch

Applies to the whole repository except `samples/` (read-only references; never edit them).

## Start of every session
1. Read `docs/ROADMAP.md` and continue from the first unticked item. 2. Read `docs/decisions/decisions.md` and `docs/specs/requirements.md`.
3. Never re-decide an owner decision (D-xx marked Owner); propose changes in the decision log instead.

## Spec Rule
- Behaviour/contract change = update `docs/specs/{overview,architecture,contracts,acceptance,changelog}.md` (and `.fa.md` pairs) in the same commit.
- Every new requirement gets an ID in `requirements.md` and an acceptance line.

## Architecture Rule (generated code)
- Layers: Domain <- Application <- Infrastructure; Api/Mcp are composition roots. No upward references.
- Ports (interfaces) live in Application (or Domain for pure domain contracts); adapters live in Infrastructure.
- Each layer registers itself via its own `DependencyInjection` extension and references only its own packages.
- One entity = one `Features/<Plural>/` folder; Commands, Queries, Actions, Events, Dtos nested inside it, one subfolder per use case.
- CQRS with MediatR 12.x; Unit of Work + Repository; repositories return materialised results through async methods; never expose `IQueryable` from ports.
- No sync-over-async, no `Console.WriteLine` for diagnostics, no hard-coded secrets, endpoints or CORS-any defaults.
- Kits: `Providers.* -> Core -> Abstractions`; Core never references a provider; services see Abstractions only (Application) and Core+Providers only in the composition root.

## Configuration Rule
- Secrets/run-time values: environment (UPPER_CASE). Non-sensitive: appsettings (PascalCase, colon). Loaded together in one step before options binding.
- Any key change updates `.env.example` and `appsettings.example.json` (generated templates and this repo's docs) in the same commit.

## Tool-code Rule (this repo)
- Run child processes with argument lists, never composed shell strings. Validate every user-supplied identifier before use or file/path creation.
- Keep files small and one responsibility per class; new commands go in `src/DotNetArch.Cli/Commands` (parsing only) with logic in `src/DotNetArch.Core`; templates in `src/DotNetArch.Core/Templates`.
- Layers (D-17): Core = implementation, Cli = commands/console, Mcp = MCP tools. Core must not reference Cli/Mcp nor use `Console`/shell directly (use `ToolHost`, D-18).
- Tests live in `tests/` per layer where needed. Do not claim a build/test passed unless it was run.

## Independence and Neutrality Rule
- A generated or adopted project must never depend on the tool: everything it needs is ordinary files in its repository; the tool keeps only `.net-arch/` (D-19). Deleting `.net-arch/` must break nothing.
- The tool contains nothing organisation-specific (D-22): organisation rules are a declarative profile. If a rule is general it belongs here, otherwise it does not.
- `fix` and generators change structure, configuration and documentation, never business code (D-21).
- New operations are defined once in the operation registry (D-20) so the CLI and MCP stay identical; mutating operations plan first and write only with apply.

## Documentation Governance Rule
- Keep `.md` + `.fa.md` pairs synchronized. Update `docs/ROADMAP.md` checkboxes in the same commit as the work.

## Roadmap/Resume Rule
- Tick an item only after it is implemented and (when possible) verified; append a short note with the commit hash.
- Before running out of budget: commit, push, and make the "Resume point" section of the roadmap accurate.

## Git Rule
- Develop on `claude/exciting-fermi-b41fue`; commit per phase with a clear message; push with `git push -u origin <branch>`; no PR unless asked.
