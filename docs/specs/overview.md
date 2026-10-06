# DotNetArch overview

DotNetArch is a cross-platform .NET global tool (`dotnet-arch`) that scaffolds opinionated Clean Architecture microservices and independent Kits, and
exposes itself as an MCP server for agents.

## Goals
1. Generate production-shaped microservices (Clean Architecture, ports/adapters, vertical slice + CQRS + UoW + repository, Docker, Git, CI, tests, optional MCP host).
2. Generate independent, provider-based Kits for external capabilities (Cache, MessageBroker, MediaStorage ...).
3. Be usable by agents through MCP.
4. Be spec-driven and agent-driven: specs and AGENTS.md govern every change.

## Non-goals
Runtime hosting, hosting of registries, live verification against third-party services (kits are verified with stubs unless stated otherwise),
unit tests for the tool itself (D-02).

## Document map
`requirements.md` (what was asked) · `decisions/decisions.md` (why) · `architecture.md` (trees) · `contracts.md` (CLI/config/MCP) ·
`acceptance.md` (how verified) · `changelog.md` · `../ROADMAP.md` (phases and resume point). Persian mirrors have `.fa.md`.

## References
`samples/corevia-identity` (microservice shape, with defects listed in D-10) and `samples/MediaStorage` (kit shape).
