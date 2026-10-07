# DotNetArch overview

DotNetArch is a cross-platform .NET global tool (`dotnet-arch`) that scaffolds opinionated Clean Architecture microservices and independent, provider-based Kits, and
exposes itself as an MCP server for agents. It is built in three layers: Cli (commands), Mcp (tool server) and Core (implementation).

## Goals
1. Generate production-shaped microservices (Clean Architecture, ports/adapters, vertical slice + CQRS + UoW + repository, Docker, Git, CI, tests, optional MCP host).
2. Generate independent, provider-based Kits for external capabilities (Cache, MessageBroker, MediaStorage ...).
3. Be usable by agents through MCP.
4. Be spec-driven and agent-driven: specs and AGENTS.md govern every change, in this repository and in everything it generates.

## Non-goals
Runtime hosting, hosting of registries, live verification of kit providers against third-party servers (they are verified with stubs), .NET SDK installation.

## Document map
`requirements.md` (what was asked) - `decisions/decisions.md` (why) - `architecture.md` (trees) - `contracts.md` (CLI, configuration, MCP) - `acceptance.md` (how it is verified) -
`changelog.md` - `../ROADMAP.md` (phases and resume point) - `openspec.yaml` / `testspec.yaml` (machine-readable). Persian mirrors use `.fa.md`.

## References
`samples/corevia-identity` (microservice shape, with the defects listed in D-10) and `samples/MediaStorage` (kit shape); both are read-only.
