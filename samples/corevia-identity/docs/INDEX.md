[فارسی](./INDEX.fa.md)

# Corevia Identity Documentation Index

This is the entry point of the documentation of the `corevia-identity` repository. The repository owns the Identity service (OTP login, token issuance and rotation, OIDC endpoints, users, roles, permissions, scopes, tenants, clients, sessions) and its MCP sibling adapter. Repository-wide agent rules live in the root `AGENTS.md`; behavior is defined by the specs below.

## Specifications

- [Specs overview](./specs/README.md): the bilingual spec set and the OpenSpec/TestSpec files.
- [Runtime overview](./specs/overview.md): responsibilities, runtime dependencies, migration invariants, MCP profile.
- [Contracts](./specs/contracts.md): HTTP, session, OTP send, MCP, authorization, and tool contracts.
- [Acceptance](./specs/acceptance.md): testable scenarios, including the MCP acceptance mapping.
- [Changelog](./specs/changelog.md): versioned contract and migration changes.
- [MCP flow verification](./specs/mcp-flow-verification.md) and [MCP runtime implementation evidence](./specs/mcp-runtime-implementation-evidence.md): what was verified for the MCP adapter and how to run it.

## Roadmaps and decisions

- [Identity roadmap](./roadmaps/roadmap.md): current phase and definition of done.
- [Repository migration roadmap](./roadmaps/services/identity-service-repo-migration-roadmap.md): Phase C steps and tracked follow-ups.
- [MCP adapter roadmap](./roadmaps/services/identity-service-mcp-roadmap.md) and [MCP decision log](./decisions/identity-service-mcp-decision-log.md): the history of the MCP sibling.

## Integrations

- [Integrations index](./integrations/README.md) and the [NopCommerce M2M integration guide](./integrations/NOPCOMMERCE_M2M_INTEGRATION.md).
