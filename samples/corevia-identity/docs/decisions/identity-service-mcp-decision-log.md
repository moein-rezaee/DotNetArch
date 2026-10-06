[فارسی](./identity-service-mcp-decision-log.fa.md) | [Roadmap](../roadmaps/services/identity-service-mcp-roadmap.md) | [Identity Specs](../specs/README.md)

> Historical record imported from the corevia-market monorepo on 2026-10-04 (Phase C step 5b). Command lines and paths such as `IdentityService/...` and `corevia-market/...` describe the monorepo layout at the time; in this repository the same projects live at the repo root and the MCP values are under `.corevia/` (see `.corevia/mcp/identity.values.yaml`). The phase execution values referenced here remain in corevia-market.

# IdentityService MCP Architecture Decisions

## Purpose

This decision log records the architecture and security choices that govern the IdentityService MCP sibling adapter before implementation and rollout.

## Scope

- MCP adapter boundary and Clean Architecture dependencies.
- Identity context, authentication, authorization, delegation, tenant policy, approval, audit, and redaction.
- Atomic/business tools, transport, local/cloud use, customer-server deployment, and future enterprise federation.

## Prerequisites

- Read the [IdentityService MCP Roadmap](../roadmaps/services/identity-service-mcp-roadmap.md) and IdentityService specs before changing a decision.
- Record a decision outcome only after explicit product/architecture approval.

## Configuration

- Do not record secrets, tokens, credentials, customer data, or private runtime endpoints.
- Evidence must point to source specs, roadmap phases, tests, or redacted reports.

## Decision Records

### DEC-ISMCP-001 - Sibling adapter and Application boundary

Status: Accepted

Options:

- **A: sibling adapter:** `IdentityService.Mcp` calls Application use cases/MediatR handlers directly and reuses Domain policies through Application boundaries.
- **B: HTTP mirror:** MCP calls controllers or the Identity HTTP API and exposes endpoint-shaped tools.

Decision:

- Option A is selected.
- Option B is rejected because it couples the agent surface to HTTP routes, encourages endpoint dumping, weakens tool-level policy, and creates a bypass/secret-leak risk.
- MCP must not call controllers, HTTP endpoints, Gateway/Ocelot routes, EF Core, repositories, SQL, or `IdentityService.Api` directly.
- Domain and Application remain independent from both transport adapters.

Consequence:

- The MCP project is a first-class sibling and requires explicit Application-facing context/policy contracts.

### DEC-ISMCP-002 - Deployment-neutral current modes and optional Gateway

Status: Accepted / Future Gateway Deferred

Options:

- **A: local/customer-server direct MCP:** use `stdio` or safe loopback locally and authenticated HTTPS/private HTTP on a customer server.
- **B: mandatory central Gateway:** route every MCP request through Corevia infrastructure.

Decision:

- Option A is selected for the current roadmap.
- A central Corevia MCP Gateway is future optional infrastructure, not a dependency of any current installation.
- If later implemented, Gateway is a relay/policy/routing layer using an outbound secure connector or mTLS; it must not automatically become a customer-data store or bypass tenant policy.

Consequence:

- Offline/local customers can consume MCP without Internet, public DNS, inbound tunnels, or a central service.

### DEC-ISMCP-003 - Server-derived identity context and modes

Status: Accepted

Decision:

- Every call creates a server-side `McpExecutionContext` containing Actor, Subject, Tenant, Mode, tool/version, required/effective scopes, risk, approval, and correlation id.
- Actor and Subject are never accepted from prompt text, natural-language instructions, arbitrary metadata, or a bare subjectId.
- Self mode resolves Actor and Subject to the current customer and rejects userId/tenantId selectors for other identities.
- Admin mode derives Actor from validated human/service/agent authentication and resolves a structured Subject only after tenant/policy authorization.
- Delegated mode uses a short-lived, signed, audience-bound context limited by expiry, audience, tenant, allowed tools/scopes, and nonce/anti-replay controls; delegation cannot escalate permissions.
- Security mode is separate and covers Client/Scope/Permission/Secret administration; Security writes are disabled by default in MVP.

Required scopes:

```text
identity.mcp.self.read
identity.mcp.self.write
identity.mcp.admin.read
identity.mcp.admin.write
identity.mcp.security.read
identity.mcp.security.write
```

Consequence:

- Authentication, Authorization, and Tool Policy must all enforce the same mode/scope/tenant boundary. Prompt instructions are not an authorization layer.

### DEC-ISMCP-004 - Atomic and business tool surface

Status: Accepted

Decision:

- MCP exposes both bounded Atomic tools and goal-oriented Business tools.
- Each tool declares `kind`, `audience`, `risk`, `approvalRequired`, `requiredScopes`, and contract version.
- Business tools may orchestrate multiple Application use cases but may not duplicate Domain rules.
- Atomic tools must be capability-shaped and reviewed; they must not become an unreviewed controller dump.
- Generic `call_identity_api`, arbitrary endpoint/SQL/repository tools, and raw token/secret tools are forbidden.
- Tool output uses purpose-built redacted DTOs. Raw access/refresh tokens, client secrets, OTP, JWT signing material, database credentials, and unmasked sensitive payloads are never returned.

Consequence:

- The MVP starts with an explicit, mostly read-only allowlist; writes and sensitive operations are enabled only through their policy and approval contracts.

### DEC-ISMCP-005 - Transport, offline use, and cloud connector boundary

Status: Accepted

Decision:

- Local `stdio` is preferred.
- Local HTTP is an authenticated loopback-only fallback on `127.0.0.1`/`::1`; public bind, public DNS, NAT, port forwarding, and unauthenticated reverse proxying are forbidden.
- Customer-server access uses authenticated HTTPS or equivalent private-network HTTP controls.
- Cloud AI cannot directly reach an offline/local MCP; it can use it only through a trusted local MCP client/desktop connector with customer consent, redaction, and egress policy.
- Sensitive mutations require approval bound to the exact tool/action/resource; natural-language confirmation alone is not approval.
- Audit includes Actor, Subject, Tenant, Mode, tool/version, scopes, decision, approval, result, and correlation id with redaction.

Consequence:

- The same capability/security contract applies to local and server installation; only transport/deployment configuration differs.

### DEC-ISMCP-006 - Future AD/LDAP, Keycloak, and enterprise Gateway boundary

Status: Deferred / Contract Recorded

Decision:

- The future enterprise path is:

```text
AD/LDAP -> Keycloak User Federation -> OIDC/OAuth2 -> Identity MCP / optional future MCP Gateway
```

- MCP must not bind directly to AD/LDAP.
- Keycloak/AD federation and managed Gateway are future optional integrations and are not current runtime prerequisites.

Consequence:

- Enterprise federation requires a new decision and implementation phase; it cannot be smuggled into the local/customer-server MVP.

## Run / Usage

- Before each dependent roadmap phase, review the relevant decision status and evidence.
- A change to a selected decision requires a new decision record and impact update in the roadmap and Identity specs.

## Validation / Verification

- Every record has status, options or alternatives, decision, consequence, and roadmap/spec impact.
- Accepted decisions are referenced by an implementation phase.
- No raw secrets or customer data appear in this file.
- English/Persian decision logs remain semantically aligned.

## Troubleshooting

- If a tool requires API/controller/EF/repository access, return to `DEC-ISMCP-001` and redesign the Application boundary.
- If a deployment request requires a mandatory Gateway or public localhost exposure, return to `DEC-ISMCP-002`/`DEC-ISMCP-005` and open a new decision.
- If identity comes from prompt text or a bare subjectId, fail closed and return to `DEC-ISMCP-003`.
- If a new enterprise provider is proposed, keep it deferred until the AD/LDAP -> Keycloak -> OIDC/OAuth2 boundary is explicitly designed and approved.

## Change Log

- 2026-08-13: Recorded the approved IdentityService MCP sibling, security, tool, deployment, and future enterprise decisions.

## Ownership

Product Engineering / IdentityService team
