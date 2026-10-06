[فارسی](./overview.fa.md)

# IdentityService Runtime Migration Overview

> Status: Active implementation baseline; controlled pilot pending
> Version: 0.11.1
> Owner: IdentityService

## Scope
- Service: `IdentityService`
- Default port: `5270`
- API prefix: `v1/api`
- Runtime migration target: centralized config via Consul, centralized secrets via Vault, bootstrap merge via `AddConfigLoaderExtension(...)`

## Responsibilities
- Preserve OTP login and token/session lifecycle behavior while moving runtime values out of local files.
- Keep startup order deterministic for centralized runtime providers.
- Maintain Consul service discovery registration and healthy `/health` checks.
- Seed dedicated `invoice-service` and `pos-service` M2M clients for the POS financial confirmation flow.
- Keep the `woosync-service` M2M client contract available for the independent WooCommerce provider in `corevia-sync`.

- Return an explicit accepted/queued result from the OTP send endpoint so clients do not infer success from an empty response.

## Runtime Dependencies
- Consul (`Corevia.Kit.Config`, namespace `ConfigCenterExtension`)
- Vault (`Corevia.Kit.Secrets`, namespace `SecretStoreExtension`)
- Config loader (`Corevia.Kit.ConfigLoader`; the Config and Secrets providers are registered before `AddConfigLoaderExtension(...)`)
- Database provider adapter: Postgres by default; SQL Server is selectable through runtime configuration.
- OTPService

## Migration Invariants
- `AddConfigLoaderExtension(...)` executes before options binding and config-dependent registrations.
- `appsettings.json` keeps only non-sensitive defaults.
- `.env` keeps only bootstrap/provider connectivity values and non-sensitive local runtime values.

## Corevia MCP Standard Profile

The generic MCP architecture, security, ToolSpec, transport, migration, compatibility, rollback, and acceptance rules are inherited from the [Corevia MCP Source Standard](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/README.md) and [normative MCP contracts](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/contracts.md). The machine-readable Identity profile is [`.corevia/mcp/identity-service.yaml`](../../.corevia/mcp/identity-service.yaml).

Identity-specific profile:

- `IdentityService.Api` and `IdentityService.Mcp` are peer transport adapters over Application/Domain; MCP calls Application use cases/MediatR directly.
- Identity scopes are `identity.mcp.self.read`, `identity.mcp.self.write`, `identity.mcp.admin.read`, `identity.mcp.admin.write`, `identity.mcp.security.read`, and `identity.mcp.security.write`.
- `self` is the current customer context; `admin` is an internal tenant-scoped operator/agent context; `security` is separate and disabled by default in the MVP. Delegation is a signed, short-lived customer context under self, not a fourth mode or an escalation.
- Identity exposes a complete current capability catalog as both atomic and business Tools: 34 Atomic Tools and 2 Business Tools cover self profile/session, user, role, tenant, permission, scope, client, and relationship operations. Generic API/endpoint/SQL/repository proxy Tools are forbidden.
- Local customer installation uses authenticated loopback HTTP (default); `stdio` is development-only. Customer-server installation uses authenticated HTTPS or equivalent private-network controls. A central Gateway is future optional infrastructure, not a current runtime dependency.
- Cloud AI reaches local MCP only through a trusted local MCP client/Desktop Connector and customer-approved egress. The future enterprise path is AD/LDAP → Keycloak User Federation → OIDC/OAuth2; MCP never binds directly to AD/LDAP.
- Identity's API/MCP capability catalog contains 41 one-to-one entries: 36 active authorization permissions map to usable active Tools and five protocol operations are explicitly inventory-only/non-exposable because raw token and secret exposure is forbidden. API and MCP grants remain separate and parity drift fails closed.

The service-level entry point is [the Identity MCP create-or-migrate values](../../.corevia/mcp/identity.values.yaml); it detects whether the MCP source is new or existing and routes to the governed child Pattern. Source creation is governed by [the Identity MCP source values](../../.corevia/operations/identity-service-mcp-source.values.yaml). The generic contract is validated with [the Identity MCP contract values](../../.corevia/operations/identity-service-mcp-contract.values.yaml). Runtime implementation status remains controlled by the [IdentityService MCP roadmap](../roadmaps/services/identity-service-mcp-roadmap.md).

## Runtime MCP Implementation

The first executable Identity MCP baseline is now present in `IdentityService/IdentityService.Mcp`:

- `identity_get_self_profile` and `identity_list_self_sessions` are bounded Atomic, read-only Self Tools.
- `identity_get_user_access_summary` is a Business, read-only Admin Tool that coordinates the authorized user, role, and tenant Application queries.
- `identity_revoke_user_sessions` is a Business, sensitive Admin Tool that requires an exact signed approval and calls the existing session commands directly. The remaining active admin capabilities are exposed as named Atomic Tools over the corresponding Application handlers.
- The runtime creates a server-owned execution context, enforces authenticated Actor/Subject/Tenant resolution, separate MCP scopes, Self/Admin/Delegated policy, tenant boundaries, anti-replay nonces, redacted DTOs, audit records, and approval binding.
- HTTP is the default transport (authenticated loopback, or guarded customer-server HTTPS/private HTTP); `stdio` is development-only and refused outside the Development environment. The stdio host does not open a Kestrel listener.

Local build, the complete catalog/security test suite, and the stdio `initialize`/`tools/list` protocol smoke are recorded in the [MCP runtime implementation evidence](./mcp-runtime-implementation-evidence.md). Remote CI/MR verification and the controlled customer pilot remain open release gates.

## Last Updated
- 2026-07-06

- 2026-09-03
