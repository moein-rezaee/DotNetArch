[فارسی](./README.fa.md)

# IdentityService Specs


## Files
- `overview.md`
- `contracts.md`
- `acceptance.md`
- `changelog.md`
- `mcp-flow-verification.md`
- `mcp-runtime-implementation-evidence.md`

The overview, contracts, and acceptance specs contain the Identity-specific `IdentityService.Mcp` profile, while generic MCP architecture, security, ToolSpec, transport, migration, rollback, and acceptance rules are inherited from the [Corevia MCP Source Standard](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/README.md).

The machine-readable service profile is [`.corevia/mcp/identity-service.yaml`](../../.corevia/mcp/identity-service.yaml). The single service-level entry point is [the create-or-migrate values](../../.corevia/mcp/identity.values.yaml); it detects the current source state and routes to the governed child Pattern. Source creation is governed by [the Corevia MCP source values](../../.corevia/operations/identity-service-mcp-source.values.yaml), migration/adoption by [the Identity MCP migration values](../../.corevia/mcp/identity.values.yaml), and read-only contract validation by [the MCP contract values](../../.corevia/operations/identity-service-mcp-contract.values.yaml).

The implementation plan is [IdentityService MCP Adapter Roadmap](../roadmaps/services/identity-service-mcp-roadmap.md), with decisions in [IdentityService MCP Architecture Decisions](../decisions/identity-service-mcp-decision-log.md).

The latest Identity-only flow verification is recorded in [MCP Flow Verification](./mcp-flow-verification.md) and its [Persian version](./mcp-flow-verification.fa.md). Executable runtime results are recorded in [MCP Runtime Implementation Evidence](./mcp-runtime-implementation-evidence.md) and its [Persian version](./mcp-runtime-implementation-evidence.fa.md).

## Ownership
- Service team owning `IdentityService`

## Last Updated
- 2026-08-29
