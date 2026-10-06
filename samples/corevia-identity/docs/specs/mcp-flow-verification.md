# IdentityService MCP Flow Verification

[فارسی](./mcp-flow-verification.fa.md) | [Specs Index](./README.md)

## Purpose

Record the Corevia Standard service-level MCP flow and the first executable runtime verification for `IdentityService` only.

## Scope

- Repository under test: `corevia-market`
- Service under test: `IdentityService`
- Entry point: `.corevia/operations/identity-service-mcp-create-or-migrate.values.yaml`
- Standard entry point: `mcp-service.create-or-migrate`
- No other Market service was changed or migrated in this verification.

## Fix Applied

The Identity MCP manifest previously used conceptual Application entry-point names. They were corrected to the actual Query/Command contracts:

- `GetProfileQuery`
- `GetCurrentUserSessionsQuery`
- `GetUserByIdQuery`
- `GetUserRolesQuery`
- `GetUserTenantsQuery`
- `RevokeSessionCommand`
- `RevokeOtherSessionsCommand`

Every declared source path was checked against the Identity Application tree.

## Verification Matrix

| Check | Result | Evidence |
|---|---|---|
| Service facade validation | PASS | The active manifest and `IdentityService/IdentityService.Mcp` project select `mcp-source.migrate-or-adopt`; the source values remain the governed service profile. |
| MCP manifest contract | PASS | Identity modes, scopes, tools, transports, audit, redaction, and boundaries validate. |
| MCP source contract | PASS | Sibling adapter, Application-only entry, no API mirror, no direct data access, and deployment profiles validate. |
| Identity roadmap contract | PASS | Bilingual roadmap and phase values validate. |
| First open phase plan | PASS | Reports Phase 7 (Test, Documentation, and Release Readiness); implementation phases 1–6 have executable evidence. |
| Roadmap status | PASS | No decision override is required. |
| Generated Skill drift check | PASS (scoped) | `corevia-mcp-service-create-or-migrate` is current and generated from the Pattern. The all-pattern Standards workspace check is not a release result for this Market MR because unrelated Standards worktree changes leave other generated Skills stale. |
| Generated Skill validation | PASS (scoped) | The selected service Skill metadata and files are valid; unrelated Standards Skill drift is kept outside this Identity change. |
| Migration branch fixture | PASS | The generic facade selects `mcp-source.migrate-or-adopt` for an active-source fixture. |
| Apply gate | EXPECTED BLOCK | The service facade remains read-only; source mutation is not performed through a facade `apply` shortcut. |
| Runtime implementation evidence | PASS | The sibling project builds, the complete catalog/security suite passes, and stdio `initialize`/`tools/list` smoke completes with 36 active tools (34 Atomic and 2 Business). HTTP negative smoke also passes on loopback. |
| API/MCP permission parity | PASS | 41 API capability entries have one-to-one mappings: 36 authorization permissions expose usable active Tools, and 5 protocol operations remain explicit non-exposable inventory bindings. API/MCP grants are separate and parity drift is fail-closed. |
| External release gate | PENDING | Remote CI/MR, customer pilot, publication, production deployment, and future managed Gateway remain separate gates. |

## Commands

```text
corevia-standards/bin/corevia-run validate --pattern mcp-service.create-or-migrate --values corevia-market/.corevia/operations/identity-service-mcp-create-or-migrate.values.yaml
corevia-standards/bin/corevia-run validate --pattern mcp-contract.validate --values corevia-market/.corevia/operations/identity-service-mcp-contract.values.yaml
corevia-standards/bin/corevia-run validate --pattern mcp-source.create-or-update --values corevia-market/.corevia/operations/identity-service-mcp-source.values.yaml
corevia-standards/bin/corevia-run validate --pattern roadmap.create-or-update --values corevia-market/.corevia/operations/identity-service-mcp-roadmap.values.yaml
corevia-standards/bin/corevia-run plan --pattern roadmap-phase.execute --values corevia-market/.corevia/operations/identity-service-mcp-roadmap-phase.values.yaml
corevia-standards/bin/corevia-run status --pattern roadmap-phase.execute --values corevia-market/.corevia/operations/identity-service-mcp-roadmap-phase.values.yaml
```

## Boundary

This verification proves the governance/routing flow, the Identity-specific contract, and the locally verified `IdentityService/IdentityService.Mcp` runtime baseline. It does not claim remote CI/MR completion, customer transport acceptance, package publication, production deployment, central Gateway availability, or enterprise federation. Runtime implementation, customer rollout, and external mutations remain separate controlled gates.

The runtime evidence is maintained in [MCP Runtime Implementation Evidence](./mcp-runtime-implementation-evidence.md).

## Next Action

Complete the remaining Phase 7 checks through `roadmap-phase.execute`, especially remote CI/MR verification and repository/documentation validation. Only after those checks pass may Phase 7 be marked complete and Phase 8 be opened for a separately approved customer pilot. Do not call `apply` on the service facade as a workaround.

## Change Log

- 2026-08-28: Recorded the Identity-only service-facade verification and corrected Application entry-point mapping.
- 2026-08-29: Reconciled the report with the complete 41-entry API/MCP parity catalog, 36 active-tool protocol smoke, explicit non-exposable protocol bindings, and the remaining Phase 7 release gates.

## Ownership

IdentityService team
