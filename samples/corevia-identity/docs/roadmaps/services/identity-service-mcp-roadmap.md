[فارسی](./identity-service-mcp-roadmap.fa.md) | [Parent](../roadmap.md) | [Identity Specs](../../specs/README.md) | [Decision Log](../../decisions/identity-service-mcp-decision-log.md)

> Historical record imported from the corevia-market monorepo on 2026-10-04 (Phase C step 5b). Command lines and paths such as `IdentityService/...` and `corevia-market/...` describe the monorepo layout at the time; in this repository the same projects live at the repo root and the MCP values are under `.corevia/` (see `.corevia/mcp/identity.values.yaml`). The phase execution values referenced here remain in corevia-market.

# IdentityService MCP Adapter Roadmap

## Endpoint / Definition of Done

- `IdentityService.Mcp` is implemented as a sibling transport adapter next to `IdentityService.Api`.
- MCP invokes approved Identity Application use cases/MediatR handlers directly; it never mirrors or calls the HTTP API, controllers, Gateway/Ocelot routes, EF Core, or repositories directly.
- Self, admin, and security access are enforced by authenticated server context, authorization, and Tool Policy; delegated customer access is a signed execution context under self, never a fourth mode or escalation.
- MCP scopes, tenant boundaries, Actor/Subject rules, approval, audit, redaction, and the atomic/business tool model are implemented and covered by positive and negative tests.
- The current release supports deployment-neutral local and customer-server modes:
  - local customer installation with `stdio` preferred and authenticated loopback HTTP as a fallback;
  - customer-server installation with authenticated HTTPS or private-network HTTP with equivalent controls.
- A Corevia central MCP Gateway is not a current runtime dependency. A future managed gateway remains an optional relay/policy layer behind an outbound secure connector or mTLS boundary.
- A cloud-hosted AI can use an offline/local MCP only through a trusted MCP client or desktop connector running on the customer machine; it cannot reach the local MCP directly.
- AD/LDAP integration is future-only and follows `AD/LDAP -> Keycloak User Federation -> OIDC/OAuth2 -> Identity MCP`; MCP never binds directly to AD/LDAP.
- All implementation, tests, runtime configuration classification, bilingual specs, changelog, version impact, and evidence are complete before the roadmap is closed.

## Roadmap Links

- Parent: [Market Roadmap Entrypoint](../roadmap.md)
- Previous: [Market Module-First Communication Roadmap](https://gitlab.getcorevia.ir/corevia/market/-/blob/develop/docs/roadmaps/services/market-module-first-communication-roadmap.md)
- Next: TBD
- Depends On: [Corevia Standards](http://gitlab.getcorevia.ir/corevia/standards/-/blob/develop/README.md), [Corevia MCP Source Standard](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/README.md), [MCP service create-or-migrate Pattern](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/patterns/mcp-service.create-or-migrate/README.md), [Identity MCP create-or-migrate values](../../../.corevia/mcp/identity.values.yaml), [Identity MCP source values](../../../.corevia/operations/identity-service-mcp-source.values.yaml), [Identity MCP migration values](../../../.corevia/mcp/identity.values.yaml), [Identity MCP Manifest](../../../.corevia/mcp/identity-service.yaml), [IdentityService Specs](../../specs/README.md), [IdentityService MCP Decisions](../../decisions/identity-service-mcp-decision-log.md)
- Updates: `IdentityService`, `IdentityService.Application`, `IdentityService.Api`, `IdentityService.Mcp`, `.corevia/mcp/identity-service.yaml`, `.corevia/operations/identity-service-mcp-source.values.yaml`, and IdentityService bilingual specs

## Follows

```yaml
follows:
  corevia_standards: corevia-standards@0.1.0
  roadmap_governance: roadmap-governance-standard@0.1.0
  roadmap_pattern: roadmap.create-or-update@0.3.0
  phase_execution: roadmap-phase.execute@0.1.0
  service_mcp_entrypoint: mcp-service.create-or-migrate@0.1.0
```

## Purpose

This roadmap turns the approved Identity MCP architecture into a safe, testable, and deployable sibling adapter. It preserves the existing Clean Architecture and CQRS boundaries while allowing an internal administrative agent or a customer-authorized agent to consume Identity capabilities through MCP.

## Scope

- Source inventory of the current Identity API, Application use cases, Domain entities, Infrastructure registration, JWT/client/scope/permission/session/tenant behavior, and existing audit hooks.
- A new `IdentityService.Mcp` adapter that is independent from the HTTP API and deployment location.
- Server-created `McpExecutionContext` with `Actor`, `Subject`, `Tenant`, `Mode`, scopes, risk, approval, tool version, and correlation data.
- Self, admin, delegated, and security authorization modes with explicit MCP scopes and tenant boundaries.
- A combined atomic and business-level tool contract with versioned schemas, redacted DTOs, risk, audience, approval, and required scopes.
- Local `stdio`/loopback transport, customer-server HTTPS/private-network transport, local cloud-client boundary, and future managed-gateway boundary.
- Unit, component, integration, transport, security-negative, documentation, and rollout evidence.

Out of scope for the current release:

- Making a central Corevia MCP Gateway a required dependency.
- Reverse-proxying all MCP calls through the existing HTTP API or exposing an endpoint mirror.
- Direct MCP access to EF Core, repositories, SQL, database credentials, or infrastructure secrets.
- Direct AD/LDAP bind, Keycloak deployment, or enterprise federation implementation.
- Exposing OTP send/verify, refresh-token issuance, raw token material, client secrets, JWT signing material, or security write tools by default in the MVP.
- Package publish, production deployment, customer data migration, or external provider mutation without their separate approval gates.

## Prerequisites

- `corevia-standards` is available beside `corevia-market` in the workspace.
- The service-level MCP entry point is `corevia-market/.corevia/operations/identity-service-mcp-create-or-migrate.values.yaml`; it must be validated before selecting the create or migration child Pattern.
- The pinned Corevia MCP source contract is consumed through `corevia-market/.corevia/mcp/identity-service.yaml`, created through `mcp-source.create-or-update`, and validated by `mcp-contract.validate`.
- The current Identity architecture/spec changes are available in `IdentityService/AGENTS.md`, `IdentityService/docs/specs/overview.md`, `contracts.md`, and `acceptance.md`.
- Existing local changes are inventoried before implementation; unrelated user work is preserved.
- Any runtime/config/deployment change is evaluated for project-version and real-environment version impact.
- No MCP phase is executed until its `roadmap-phase.execute` plan is produced and the human approves that exact phase.

## Configuration

- Canonical roadmap values: `corevia-market/.corevia/operations/identity-service-mcp-roadmap.values.yaml`.
- Canonical phase values: `corevia-market/.corevia/operations/identity-service-mcp-roadmap-phase.values.yaml`.
- Canonical service-level MCP routing values: `corevia-market/.corevia/operations/identity-service-mcp-create-or-migrate.values.yaml`.
- Non-sensitive MCP settings belong in the Corevia runtime configuration path through Consul/`ConfigCenterExtension` and `ConfigLoaderExtension`.
- MCP credentials, signing keys, client secrets, approval secrets, and any other sensitive value belong in Vault/`SecretStoreExtension`; they must not be hard-coded in `appsettings.json`, roadmap files, tool schemas, or `.env` documentation.
- `AddConfigLoaderExtension(...)` must remain before options binding and before any registration that consumes MCP configuration.
- Local `stdio` must not require Consul, Vault, a central gateway, or Internet connectivity to serve a local customer MCP client, but any optional provider configuration must still follow the service runtime standard.
- Shared generic MCP rules are owned by `corevia-standards`; reusable runtime abstractions, if later proven necessary, belong in an approved `corevia-kit` package; Bridge owns only approved runtime/deployment executors.

## Pattern Detection Gate

Execution Pattern: `roadmap-phase.execute`

Execution Values: `corevia-market/.corevia/operations/identity-service-mcp-roadmap-phase.values.yaml`

Before every phase, the agent must run the Corevia Standard plan command, report the first open phase, risks, files, verification, and separate gates, then wait for explicit approval. Approval of a phase never approves commit/push, package publish, deploy, Gateway mutation, Consul/Vault mutation, or destructive actions.

## Remaining closure gates

The implementation baseline is complete locally, but this roadmap is not closed until the following delivery gates are recorded:

- [ ] Commit and push the Standards, Bridge, and Market branches without absorbing unrelated agent changes.
- [ ] Open and merge the dependency-ordered Merge Requests: Standards -> `main`, Bridge -> `main`, and Market/Identity -> `develop`.
- [ ] Verify green remote CI for all three repositories, including authenticated private-package restore where the pipeline requires Nexus.
- [ ] Run the approved Phase 7 remote/release evidence and a real Identity MCP smoke against the selected local or customer-server transport; local contract validation alone is insufficient.
- [ ] Open Phase 8 separately for customer pilot, publication, and production deployment. Each requires its own approval, health/readback, redacted evidence, and rollback record.
- [ ] Keep the reusable future-service executor as an explicit follow-up: until the child `apply` executor is approved and implemented, `mcp-service.create-or-migrate` remains a validation/plan facade and must report `extension-needed` for mutation.
- [ ] Keep the central Gateway, AD/LDAP, Keycloak federation, and other future integrations optional; they must not be used to close the current roadmap.

## Minimum Prompt Contract

For a fresh context:

```text
Follow Corevia Standard and this roadmap. Plan only the first open phase and wait for explicit approval before implementation:
corevia-market/docs/roadmaps/services/identity-service-mcp-roadmap.fa.md
```

For implementation after a phase-specific approval:

```text
Follow Corevia Standard and the approved phase contract for:
corevia-market/docs/roadmaps/services/identity-service-mcp-roadmap.fa.md
Execute only that phase. Keep publish, deploy, commit/push, external mutation, and destructive actions behind separate approvals.
```

## Run / Usage

- Validate roadmap values:
  `corevia-standards/bin/corevia-run validate --pattern roadmap.create-or-update --values corevia-market/.corevia/operations/identity-service-mcp-roadmap.values.yaml`
- Plan the first open phase:
  `corevia-standards/bin/corevia-run plan --pattern roadmap-phase.execute --values corevia-market/.corevia/operations/identity-service-mcp-roadmap-phase.values.yaml`
- Use `corevia-run apply` only after approval for the exact phase.
- Record phase evidence under `corevia-bridge/.work/roadmap-phase-executions/identity-service-mcp-roadmap/` and update both roadmap languages only with evidence.

## Architecture Decision Brief

- Problem: Identity capabilities must be consumable by agents without creating an insecure copy of the HTTP API, leaking identity secrets, or forcing every customer installation through a central service.
- Option A: Add `IdentityService.Mcp` as a sibling adapter that calls Application use cases directly. This preserves Clean Architecture, supports local/offline and customer-server deployment, and gives MCP its own policy boundary.
- Option B: Implement MCP as an HTTP client or endpoint mirror over `IdentityService.Api`. This reuses routes quickly but couples MCP to transport details, makes authorization/tool contracts ambiguous, and creates a broad API-to-agent exposure risk.
- Option C: Require a central Corevia MCP Gateway for every installation. This simplifies managed routing but blocks offline/local customers and introduces an unnecessary runtime dependency at this stage.
- Recommendation: Option A now; keep Option C as a future optional managed mode.
- Selection: Option A is selected. Option B is rejected. Option C is deferred and must not block local or customer-server delivery.

## Decision Records

The detailed records are maintained in the [IdentityService MCP Decision Log](../../decisions/identity-service-mcp-decision-log.md):

- `DEC-ISMCP-001`: sibling adapter and direct Application/MediatR boundary.
- `DEC-ISMCP-002`: deployment-neutral local/customer-server modes; central Gateway deferred.
- `DEC-ISMCP-003`: server-derived Actor/Subject, Self/Admin/Security modes, signed delegated customer context, and tenant boundaries.
- `DEC-ISMCP-004`: combined atomic/business tools and mandatory tool metadata.
- `DEC-ISMCP-005`: local transport, cloud connector, offline boundary, and redaction.
- `DEC-ISMCP-006`: future AD/LDAP, Keycloak Federation, OIDC/OAuth2, and optional managed Gateway boundary.

## Non-Negotiable MCP Invariants

The generic invariants in this section are inherited from the [Corevia MCP Source Standard](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/contracts.md), [acceptance baseline A01–A16](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/acceptance.md), and [migration contract](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/migration.md). The Identity-specific manifest is [`.corevia/mcp/identity-service.yaml`](../../../.corevia/mcp/identity-service.yaml). This roadmap maps and tests those rules for Identity; it must not weaken or redefine them.

### Execution context and identity

Every call creates a server-side `McpExecutionContext` containing:

- `Actor`: the authenticated human, service client, or agent client that initiated the call.
- `Subject`: the user, tenant, or session on which the operation acts.
- `Tenant`: the resolved tenant boundary.
- `Mode`: `self`, `admin`, or `security`; delegated customer access is a signed execution context under `self`.
- tool name/version, required/effective scopes, risk, approval state, and `CorrelationId`.

`Actor` and `Subject` must never come from prompt text, natural-language instructions, caller-provided arbitrary metadata, or a bare `subjectId`. The server derives them from validated authentication and policy context.

### Authorization modes and scopes

| Mode | Actor/Subject rule | Scope boundary | MVP rule |
|---|---|---|---|
| `self` | Actor and Subject are the current authenticated customer | `identity.mcp.self.read` / `identity.mcp.self.write` | No `userId`, `tenantId`, or cross-user selector is accepted |
| `admin` | Actor comes from validated human/service/agent authentication; Subject is resolved and authorized | `identity.mcp.admin.read` / `identity.mcp.admin.write` | Every operation is tenant-scoped; missing/ambiguous tenant is denied |
| `delegation under self` | Subject comes from a signed, short-lived, audience-bound delegation context | Narrow granted self capability; delegation cannot escalate | Expiry, audience, tenant, allowed tools/scopes, nonce/anti-replay are checked |
| `security` | Administrative security context only | `identity.mcp.security.read` / `identity.mcp.security.write` | Client/Scope/Permission/Secret management is disabled by default in MVP |

The six MCP scope families are separate from existing API scopes. Each API authorization permission also has its own granular MCP permission/scope mapping (36 active authorization mappings plus five explicit non-exposable protocol-operation inventory bindings):

```text
identity.mcp.self.read
identity.mcp.self.write
identity.mcp.admin.read
identity.mcp.admin.write
identity.mcp.security.read
identity.mcp.security.write
```

Authentication, Authorization, and Tool Policy must all enforce these rules. Prompt instructions, tool descriptions, user metadata, or natural-language confirmation never grant access.

### Delegation, approval, audit, and redaction

- A delegated context is short-lived, signed, audience-bound, tenant-bound, and limited to named tools/scopes; expired, wrong-audience, over-broad, unsigned, or replayed contexts are rejected.
- Sensitive writes require approval bound to the exact tool, action, and resource. A conversational “yes” is not an approval artifact.
- Audit records include Actor, Subject, Tenant, Mode, tool name/version, required/effective scopes, authorization decision, approval id/status, result status, and correlation id.
- Audit and tool output are redacted. Raw access/refresh tokens, client secrets, OTP codes, JWT signing material, database credentials, and unmasked sensitive payloads are never returned or recorded.

### Tool model

MCP exposes both shapes:

- **Atomic:** one bounded capability close to an Application use case, such as `identity_get_self_profile`, `identity_list_self_sessions`, or `identity_get_user`.
- **Business:** one user/agent goal that may coordinate multiple Application use cases, such as `identity_get_user_access_summary` or `identity_revoke_user_sessions`.

Every tool declares at least:

```text
kind: atomic | business
audience: self | admin
risk: read | write | sensitive
approvalRequired: true | false
requiredScopes: [...]
version: <contract-version>
```

Business tools may orchestrate Application use cases but may not duplicate Domain rules. Atomic tools may not be an unreviewed one-to-one dump of controllers. Generic `call_identity_api`, arbitrary endpoint execution, arbitrary SQL, raw repository access, and raw secret/token tools are forbidden.

### Current deployment boundary

```text
Local customer:
  Trusted MCP Client --stdio (preferred)--> IdentityService.Mcp
                                      └--> IdentityService.Application -> Infrastructure

Customer server:
  Authenticated MCP Client --HTTPS/private HTTP--> IdentityService.Mcp
                                                   └--> Application -> Infrastructure

Future managed mode (not current):
  Agent -> optional Corevia MCP Gateway -> outbound secure connector/mTLS -> Customer MCP
```

For local HTTP fallback, the server binds only to `127.0.0.1`/`::1`, keeps authentication and authorization enabled, validates Host/Origin where applicable, uses per-process/client sessions and short-lived connection credentials, and is never exposed through public DNS, NAT, port forwarding, or an unauthenticated reverse proxy. Cloud AI reaches a local MCP only through the trusted local client/desktop connector, with customer consent, egress policy, and redaction.

### Future enterprise federation boundary

```text
Active Directory (AD/LDAP)
        -> Keycloak User Federation
        -> OIDC/OAuth2
        -> Identity MCP / optional future MCP Gateway
```

MCP must not bind directly to AD/LDAP. Keycloak/AD and a central Corevia MCP Gateway are future optional integrations, not current local/customer-server runtime prerequisites.

## Phases

### Phase 1 - Source Inventory and Boundary Baseline

Status: Completed

Outcome:
- A reviewable inventory proves how the current Identity source can host a sibling MCP adapter without crossing Clean Architecture boundaries.

Deliverables:
- Project/reference map for Api, Application, Domain, Infrastructure, and the planned Mcp project.
- Use-case catalog for Users, Profile, Sessions, UserRoles, UserTenants, Tenants, Roles, Permissions, Scopes, Clients, and Client/Scope/Permission joins.
- Authentication, JWT, M2M client, tenant, CORS, discovery, runtime-provider, and audit baseline.
- Initial tool candidate matrix classified as self/admin/security, atomic/business, risk, and likely Application handler.
- Explicit gap list for missing approval, delegation, context, redaction, and MCP transport capabilities.

Tasks:
- [x] Inspect the solution/project references, `Program.cs`, controllers, Application handlers, Domain interfaces, Infrastructure registration, and existing tests.
- [x] Map existing Application commands/queries to candidate self and admin capabilities without designing endpoint mirrors.
- [x] Inventory current JWT claims, client/scope/permission/role/tenant/session behavior and identify the server-owned Actor/Subject inputs.
- [x] Audit current API CORS and transport behavior so MCP does not inherit the existing broad API exposure policy.
- [x] Classify every proposed MCP runtime value as Consul config, Vault secret, environment/bootstrap value, or local-only default.
- [x] Record the inventory and unresolved implementation gaps in the phase evidence.

Acceptance:
- [x] Every candidate capability has an owner, Application entry point, audience, risk, data sensitivity, and explicit exclusion or next phase.
- [x] No MCP implementation begins from a controller or HTTP route list.
- [x] Existing local changes and current runtime/version state are recorded without mutation.

Verification:
- `rg --files IdentityService`
- `rg -n "AddAuthentication|AddAuthorization|JwtBearer|AddMediatR|AllowAnyOrigin|Controller|Client|Scope|Permission|Tenant|Session" IdentityService --glob '*.cs' --glob '*.csproj' --glob 'appsettings*.json'`
- `dotnet sln IdentityService/IdentityService.sln list` (or the repository's canonical solution-list command)
- Source/reference review and no-secret scan

Evidence:
- First-phase report under `corevia-bridge/.work/roadmap-phase-executions/identity-service-mcp-roadmap/`.
- Inventory references in the IdentityService MCP decision/spec documents.

### Phase 2 - Architecture and Contract Freeze

Status: Completed

Outcome:
- The sibling boundary, execution modes, scope model, tool model, transport boundary, and future integration deferrals are accepted as implementation contracts.

Deliverables:
- Accepted decision records `DEC-ISMCP-001` through `DEC-ISMCP-006`.
- Synchronized English/Persian Identity specs for overview, contracts, acceptance, and changelog.
- Versioned MCP contract baseline for `McpExecutionContext`, tool metadata, authorization outcomes, approval, audit, and redaction.

Tasks:
- [x] Confirm Option A sibling adapter and reject HTTP endpoint mirroring.
- [x] Confirm current local/customer-server deployment and defer central Gateway dependency.
- [x] Confirm Self/Admin/Security modes, signed delegation context, exact scopes, tenant policy, Actor/Subject derivation, and no prompt-based authorization.
- [x] Confirm atomic/business tools, mandatory metadata, redacted DTO policy, and MVP exclusions.
- [x] Confirm local/cloud transport safeguards and the future AD/LDAP -> Keycloak -> OIDC/OAuth2 boundary.
- [x] Record rationale, consequences, and evidence in the bilingual decision log.

Acceptance:
- [x] No architecture decision remains implicit in a prompt or implementation assumption.
- [x] The decision log explicitly records selected/rejected/deferred options and their consequences.
- [x] English/Persian specs remain semantically aligned and link to this roadmap.

Verification:
- `corevia-standards/bin/corevia-run validate --pattern roadmap.create-or-update --values corevia-market/.corevia/operations/identity-service-mcp-roadmap.values.yaml`
- Bilingual diff review for the decision log and Identity specs
- Link and no-raw-secret review

Evidence:
- `corevia-market/docs/decisions/identity-service-mcp-decision-log.md` and `.fa.md`.
- IdentityService spec changelog entry and phase execution report.

### Phase 3 - MCP Project Boundary and Composition Root

Status: Completed

Outcome:
- `IdentityService.Mcp` exists as an independently buildable sibling adapter with a clean dependency graph.

Deliverables:
- `IdentityService/IdentityService.Mcp` project targeting the repository TFM and registered in the canonical solution/build path.
- MCP composition/DI registration that can reuse Infrastructure registration at the composition boundary without referencing `IdentityService.Api`.
- Application-facing context/policy ports and protocol-to-command/query mapping boundary.
- No MCP dependency from Domain/Application back to Api or Mcp.

Tasks:
- [x] Create the Mcp project and choose the approved MCP SDK/transport package after a source/license/package review.
- [x] Add only the required project/package references; explicitly reject an `IdentityService.Api` reference.
- [x] Define the adapter composition boundary, Application context abstraction, error mapping, cancellation, and correlation propagation.
- [x] Register MCP services from the composition root while preserving `AddConfigLoaderExtension(...)` ordering and existing startup behavior.
- [x] Add a disabled-by-default MCP host/feature switch and safe local defaults without adding credentials to source config.
- [x] Add dependency and build checks that fail if Api/Controller/EF/Repository references enter the MCP adapter.

Acceptance:
- [x] `IdentityService.Mcp` builds independently and has no reference to `IdentityService.Api`.
- [x] Domain and Application remain transport-agnostic.
- [x] The adapter calls only Application contracts/handlers and shared policy abstractions; it has no direct controller, HTTP, EF, repository, or SQL path.
- [x] Existing API startup and behavior remain unchanged when MCP is disabled.

Verification:
- `dotnet list IdentityService/IdentityService.Mcp/IdentityService.Mcp.csproj reference`
- `dotnet build IdentityService/IdentityService.Mcp/IdentityService.Mcp.csproj --no-restore`
- `rg -n "IdentityService\.Api|Controller|DbContext|EntityFramework|Repository|ExecuteSql|HttpClient" IdentityService/IdentityService.Mcp IdentityService/IdentityService.Application IdentityService/IdentityService.Domain`
- Targeted tests plus existing Identity API build/test

Evidence:
- Project/reference graph, build output, and dependency-boundary test report in the phase execution artifact.

### Phase 4 - Authentication, Authorization, Execution Context, and Security

Status: Completed

Outcome:
- Every MCP call is authenticated, authorized, tenant-scoped, and auditable before an Application use case runs.

Deliverables:
- Server-created `McpExecutionContext` and trusted identity-context resolver.
- Authentication adapters for validated customer, human admin, service client, and agent client contexts.
- Authorization and Tool Policy enforcement for all six MCP scope families and the three modes (`self`, `admin`, `security`); signed customer delegation is a constrained execution context under `self`, not a fourth mode.
- Delegation validation, approval contract, audit contract, redaction policy, and fail-closed errors.

Tasks:
- [x] Define how existing JWT/M2M client claims map to Actor, Subject, Tenant, mode, and effective MCP scopes.
- [x] Implement Self mode with no user/tenant/subject selector and explicit cross-user/cross-tenant denial.
- [x] Implement Admin mode with server-resolved subject, tenant-scoped policy evaluation, and no guessed tenant.
- [x] Implement Delegated mode with signature, expiry, audience, tenant, allowed tools/scopes, nonce/anti-replay, and non-escalation checks.
- [x] Implement Security mode as a separate policy surface and keep security writes disabled by default in MVP.
- [x] Require exact approval for sensitive tool/action/resource mutations; do not treat model text or conversational confirmation as approval.
- [x] Emit redacted audit records with the required identity, policy, approval, result, tool-version, and correlation fields.

Acceptance:
- [x] Actor and Subject cannot be supplied or overridden through prompt text, arbitrary metadata, or a bare subjectId.
- [x] Expired, unsigned, wrong-audience, over-broad, cross-tenant, replayed, or missing-approval requests fail closed.
- [x] Raw access/refresh tokens, ClientSecret, OTP, signing material, database credentials, and unredacted sensitive payloads never appear in tool output or audit.
- [x] Authentication, Authorization, and Tool Policy independently enforce the same scope/mode rules.

Verification:
- Unit/component tests for each mode, scope, tenant boundary, delegation claim, approval state, and redaction rule
- Negative tests for prompt injection, arbitrary metadata, cross-user selectors, missing tenant, invalid delegation, replay, and missing exact approval
- Audit snapshot review with sensitive fields redacted

Evidence:
- Security test report, policy matrix, and redacted audit examples in the phase execution artifact and Identity acceptance spec.

### Phase 5 - Tool Contract and Application Capability Mapping

Status: Completed

Outcome:
- Identity MCP exposes a curated, versioned, business-oriented tool surface backed by existing Application capabilities.

Deliverables:
- Versioned tool registry/schema with `kind`, `audience`, `risk`, `approvalRequired`, `requiredScopes`, and contract version.
- Atomic and business tool catalog mapped to Application commands/queries and redacted DTOs.
- MVP allowlist and explicit disabled list for security writes, OTP/token issuance, raw secrets, and generic tools.
- Validation/error/pagination/correlation contract for MCP tool calls.

Tasks:
- [x] Define Self read tools for profile/access/session visibility and approved Self writes such as profile update/session revoke only where policy allows.
- [x] Define Admin read tools for user search/access/tenant membership/session visibility with tenant-scoped subjects.
- [x] Define later Admin write tools such as role/session changes as sensitive or write-risk contracts requiring exact approval where applicable.
- [x] Define business tools that orchestrate Application use cases without duplicating Domain rules; keep atomic tools bounded and reviewed.
- [x] Exclude generic `call_identity_api`, arbitrary endpoint/SQL/repository tools, raw token/secret tools, OTP login/verify/refresh tools, and Security writes from the default MVP allowlist.
- [x] Add schema validation, stable error mapping, redacted DTO mapping, versioning, and compatibility/deprecation rules.
- [x] Publish the tool contract in the bilingual Identity specs before enabling the runtime surface.

Acceptance:
- [x] Every exposed tool has an explicit audience, risk, approval flag, required scope, version, Application mapping, and redaction behavior.
- [x] The catalog contains both atomic and business tools without becoming a controller dump.
- [x] No tool can select an arbitrary user/tenant in Self mode or bypass tenant policy in Admin/Delegated mode.
- [x] Security and sensitive capabilities are disabled unless their separate policy and approval contract is enabled.

Verification:
- Tool schema/registry contract tests
- Application handler integration tests that prove MCP bypasses API controllers
- Positive Self/Admin read-only tests and negative generic-tool/raw-secret/cross-tenant tests
- Contract diff review against `IdentityService/docs/specs/contracts.md` and `.fa.md`

Evidence:
- Tool registry, mapping matrix, schema-test output, and MVP allowlist in the phase execution artifact.

### Phase 6 - Deployment-Neutral Transport and Runtime Configuration

Status: Completed

Outcome:
- The same MCP capability contract runs safely on a customer's machine or server without a central Gateway dependency.

Deliverables:
- Local `stdio` transport as the preferred path.
- Authenticated loopback HTTP fallback bound only to `127.0.0.1`/`::1`.
- Authenticated HTTPS and trusted-private-network HTTP mode for customer servers.
- Corevia runtime-provider classification for non-sensitive config, secrets, bootstrap, discovery, and local defaults.
- Local cloud-client/desktop-connector boundary and future Gateway connector contract, without implementing the Gateway now.

Tasks:
- [x] Implement and test `stdio` startup, process/session isolation, cancellation, and offline operation.
- [x] Implement loopback fallback with authentication/authorization enabled, Host/Origin validation, short-lived connection credentials, and no public bind/forwarding.
- [x] Define customer-server HTTPS/private-network transport, certificate/trust requirements, service health/version behavior, and tenant routing boundary.
- [x] Classify MCP settings through Consul/Vault/ConfigLoader rules; keep secrets out of appsettings, `.env` docs, tool schemas, and images.
- [x] Document that Cloud AI reaches local MCP only through a trusted local MCP Client/Desktop Connector with customer consent and egress policy.
- [x] Record the future managed mode as `Agent -> optional Corevia MCP Gateway -> outbound secure connector/mTLS -> Customer MCP`; do not make it current.

Acceptance:
- [x] Local `stdio` works without central Gateway, public DNS, inbound tunnel, or Internet access.
- [x] Loopback HTTP cannot bind publicly or operate without auth/authz.
- [x] Customer-server mode requires authenticated HTTPS or equivalent trusted-private-network controls.
- [x] The customer can choose local or server deployment without changing tool/security contracts.
- [x] No runtime configuration introduces a current central Gateway, AD/LDAP, or Keycloak dependency.

Verification:
- Local offline `stdio` smoke test
- Loopback bind/negative public-bind and unauthenticated-forwarding tests
- Customer-server HTTPS/private-network transport test
- Runtime-provider validator and configuration classification review
- Documentation links and deployment runbook review

Evidence:
- Transport matrix, configuration/provider report, local/server smoke reports, and redacted connector policy evidence.

### Phase 7 - Test, Documentation, and Release Readiness

Status: In Progress

Outcome:
- MCP is release-ready with objective source, security, contract, build, documentation, and compatibility evidence.

Deliverables:
- Unit, integration, component, transport, security-negative, and contract-test suite.
- Updated Identity README, AGENTS, overview/contracts/acceptance/changelog, roadmap, and decision log in English/Persian.
- Version impact record; if runtime behavior changes, synchronized project version and real environment version key.
- Release checklist that keeps package publish, deploy, customer rollout, and external mutations behind separate gates.

Tasks:
- [x] Run targeted MCP/Application/Domain tests and existing Identity API tests.
- [x] Run the full negative security matrix: prompt Actor/Subject injection, cross-user/tenant, invalid delegation, replay, missing approval, raw secret output, public bind, and missing Gateway.
- [x] Run build, dependency-boundary, schema compatibility, and local loopback/stdio smoke checks.
- [x] Run a customer-server HTTPS/private-network smoke with a provisioned certificate/trust boundary; this remains separate from the local loopback evidence.
- [x] Update bilingual specs and changelog from actual implementation evidence, not planned claims.
- [x] Run Corevia roadmap validation, phase planning/status/evidence checks, repository validator, no-secret scan, and documentation audit.
- [x] Decide version bump from actual runtime/config/deployment change; synchronize `.csproj`, real environment version, and docs only when required.

Acceptance:
- [ ] All mandatory positive and negative tests pass or have an explicitly owned blocker; no security blocker is silently waived.
- [ ] Bilingual docs, links, specs, decision records, and evidence are synchronized.
- [ ] No package publish, deploy, Gateway mutation, AD/Keycloak integration, or customer rollout is reported as complete without its own evidence and approval.

Verification:
- `dotnet test` for the affected solution/projects
- `corevia-standards/bin/corevia-run validate --pattern roadmap.create-or-update --values corevia-market/.corevia/operations/identity-service-mcp-roadmap.values.yaml`
- `corevia-standards/bin/corevia-run plan --pattern roadmap-phase.execute --values corevia-market/.corevia/operations/identity-service-mcp-roadmap-phase.values.yaml`
- `ruby ../corevia-standards/validators/corevia-standards/validate.rb --target corevia-market --check-repo`
- `bash scripts/docs/audit.sh`

Evidence:
- Final test/release report in the phase execution artifact and linked Identity specs.

### Phase 8 - Controlled Customer Pilot and Future Managed-Mode Readiness

Status: Planned

Outcome:
- A controlled customer pilot proves local and/or customer-server MCP consumption while preserving the current no-Gateway boundary and documenting the future managed extension point.

Deliverables:
- Pilot installation/runbook for local `stdio` and customer-server HTTPS/private-network modes.
- Customer consent, data-egress, redaction, credential rotation, disable/rollback, and audit review checklist.
- Pilot health/version/capability evidence and compatibility feedback.
- Future Gateway/connector interface note with no current implementation dependency.

Tasks:
- [ ] Select a pilot deployment mode and record the exact approved scope/tool allowlist.
- [ ] Install and run the MCP server with a trusted local or customer-server MCP client; do not open public localhost access.
- [ ] Verify offline local use, cloud connector boundary where applicable, tenant isolation, audit redaction, disable/rollback, and credential expiry/rotation.
- [ ] Record customer-facing limitations and any requested tools as new decision-gated roadmap work.
- [ ] Close the roadmap only after pilot evidence and all separate publish/deploy/customer approvals are recorded.

Acceptance:
- [ ] A customer can use the delivered MCP server in the selected local or server mode without a central Gateway.
- [ ] No raw credential, token, OTP, secret, cross-tenant data, or unapproved mutation is observable.
- [ ] Future Gateway/AD/Keycloak integration remains optional and does not alter the current deployment contract.

Verification:
- Pilot runbook execution and health/version checks
- Approved tool/scope audit and customer egress review
- Rollback/disable and credential-expiry test

Evidence:
- Pilot report, customer approval record, and final roadmap evidence under the Corevia Bridge work root.

## Current Execution Evidence

- Runtime source: `IdentityService/IdentityService.Mcp` is implemented as a sibling adapter; the API project is not referenced.
- Local build: `dotnet build IdentityService/IdentityService.Mcp/IdentityService.Mcp.csproj --no-restore` passed.
- Security/policy tests: `dotnet test IdentityService/IdentityService.Mcp.Tests/IdentityService.Mcp.Tests.csproj --no-restore` passed with the complete catalog/security suite and 0 failures.
- HTTP smoke: on loopback port `5291`, `/health` returned `200`, anonymous `/mcp` returned `401`, invalid `Host` returned `400`, and the process stopped cleanly.
- Customer-server profile smoke: HTTPS with a temporary certificate and private HTTP with explicit trust both returned `200` from `/health` and `401` for anonymous `/mcp`; no customer rollout is implied by this isolated test.
- Full-solution verification: the Release build passed with one pre-existing `NU1603` warning in `SepidarGateway.Api`; the affected Identity/MCP builds were warning-free.
- Identity auth integration: MCP scopes/permissions are seeded; only `identity.mcp.self.read` is assigned to the default public client; user tokens carry `tenant_id` only for one membership or one default membership, and M2M tenant binding is explicit.
- Protocol smoke: `initialize` and `tools/list` passed over `stdio`; exactly 36 active curated tools (34 Atomic and 2 Business) were published and stdout contained MCP frames only. Five protocol operations remain explicitly non-exposable inventory bindings. The generic host does not open a Kestrel listener in stdio mode.
- API/MCP parity: 41 API capability entries are mapped one-to-one; API and MCP grants are separate, every active authorization permission has a usable Tool, and new-permission/missing-mapping/orphan/restriction drift fails closed through the Corevia Standard validator and CI.
- Security coverage includes server-derived Actor/Subject, Self selector rejection, Admin scope-before-lookup, tenant boundary, delegated-subject membership, signed delegation expiry/audience/nonce/allowlist, exact approval, current-session binding, principal validation, and no generic proxy.
- Runtime configuration remains deployment-neutral: stdio is offline/local, HTTP is explicitly selected, loopback/private/public HTTPS rules are enforced, and no central Gateway is required.
- Remote CI/MR verification is still a release gate for this branch; customer pilot, package publish, production deployment, and future Gateway/AD/Keycloak work remain out of scope.
- Repository validator note: the repo-wide check reports 24 pre-existing baseline findings in unrelated legacy/configuration/documentation files and no new finding in the changed MCP files; Platform Engineering owns that baseline cleanup and it remains an external release gate.
- Documentation audit note: the full-repository audit reports two pre-existing placeholder-link failures in `docs/templates/TECHNICAL_DOCUMENT_TEMPLATE.fa.md` plus legacy section warnings; the changed Identity MCP documents have no new link/pairing failure and the CI changed-only audit remains the relevant MR gate.

## Validation / Verification

- `roadmap.create-or-update` values pass schema and link validation.
- `roadmap-phase.execute` values pass and discover only the first open phase.
- Every executable phase has one outcome, bounded deliverables, tasks, acceptance, verification, and evidence.
- English/Persian roadmap, decision log, specs, and index links are reciprocal and semantically aligned.
- No raw secret, credential, token, or customer data appears in source, docs, values, or evidence.
- Identity source dependency graph confirms no Application/Domain dependency on MCP/API adapters.
- MCP security-negative tests prove fail-closed behavior.
- `scripts/docs/audit.sh` and the Corevia repository validator are run; pre-existing unrelated warnings remain separately identified and are not silently attributed to this roadmap.

## Troubleshooting

- If the first phase cannot produce a complete source inventory, keep it open and do not create the MCP project.
- If an architecture choice changes the sibling boundary, scopes, Actor/Subject rules, deployment modes, or MVP exposure, update the decision log and obtain a new decision before implementation.
- If an MCP tool requires an API controller, raw repository, EF query, or arbitrary endpoint, stop and redesign the Application contract; do not add a transport shortcut.
- If local HTTP binds beyond loopback, authentication is disabled, or a forwarding path is unauthenticated, disable that mode and keep `stdio` as the supported local path.
- If a delegated context is missing bounds or approval is not exact, fail closed and record the redacted denial.
- If central Gateway, AD/LDAP, Keycloak, Consul, Vault, or Internet access becomes a hard runtime prerequisite for the current mode, treat it as architecture drift and open a decision instead of silently adding the dependency.
- If version/config/deployment behavior changes, stop before release until project and real-environment version governance is reconciled.

## Update Policy

Allowed:

- Add or split a phase when it has an independent observable outcome.
- Update status, evidence, tool matrix, source references, and implementation notes from verified evidence.
- Add a tool only after its audience, risk, scopes, approval, redaction, Application mapping, and tests are specified.

Requires an explicit decision:

- Make the central MCP Gateway mandatory for any current deployment.
- Add direct MCP -> HTTP API/controller, EF, repository, SQL, AD/LDAP, or raw-secret access.
- Change Self/Admin/Security semantics, signed delegation rules, scope names, tenant boundary, or Actor/Subject source.
- Expose OTP/token issuance, client secrets, signing material, Security writes, or sensitive mutation without the approved policy/approval contract.
- Replace `stdio`/authenticated local transport or customer-server HTTPS with public unauthenticated exposure.
- Change the atomic/business tool model or bypass Application/Domain rules.

## Metadata

- Last Updated: 2026-08-29
- Status: Active
- Version: 0.2.0

## Change Log

- 2026-08-13: Created the Corevia-standard IdentityService MCP sibling-adapter roadmap with explicit architecture, security, tool, deployment, evidence, and future-integration boundaries.
- 2026-08-28: Added the first real IdentityService.Mcp runtime, direct Application/MediatR execution, security-negative tests, stdio/HTTP transport boundary, and local execution evidence; Phase 7 remains open until remote CI/MR verification.
- 2026-08-29: Completed the Identity API/MCP parity baseline with 41 one-to-one catalog mappings, 36 active Tools, five explicit non-exposable protocol bindings, and reusable Standard/Bridge validation contracts.

## Ownership

Product Engineering / IdentityService team
