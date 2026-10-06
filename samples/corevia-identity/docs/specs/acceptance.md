[فارسی](./acceptance.fa.md)

# IdentityService Runtime Migration Acceptance

> Status: Active implementation baseline; controlled pilot pending
> Version: 0.11.1
> Owner: IdentityService

## Corevia MCP Standard Mapping

The reusable MCP acceptance baseline is defined by [Corevia MCP Acceptance A01–A16](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/acceptance.md). This file keeps the Identity-specific executable scenarios and maps them to that baseline; it must not weaken or replace the generic negative requirements. The machine-readable profile is [`.corevia/mcp/identity-service.yaml`](../../.corevia/mcp/identity-service.yaml), validated through [the Identity MCP contract values](../../.corevia/operations/identity-service-mcp-contract.values.yaml).

## Scenario 0: Customer Session Lifetime
- Given a customer has completed OTP verification
- When the access token expires and the browser requests a protected API
- Then the client can rotate the refresh token without forcing a new login
- And the session remains valid until the customer logs out or the session is explicitly revoked

## Scenario 1: Local Audit And Cleanup
- Given:
	- `IdentityService.Api/appsettings.json`, real `.env`, `docker-compose.yml`, `Program.cs`, and related options are audited
- When:
	- unused/duplicate local keys are removed and only bootstrap-safe local values are retained
- Then:
	- no hardcoded runtime secrets remain in local `.env`
	- appsettings keeps non-sensitive defaults only

## Scenario 2: Centralized Config And Secrets
- Given:
	- Consul prefix and Vault path are configured for `identity-service`
- When:
	- service boots with valid `CONSUL_*`, `VAULT_*`, `CONFIG_CENTER_PREFIX`, and `SECRET_STORE_PATH`
- Then:
	- JWT and IdentityClient non-sensitive config resolves from Consul
	- DB/JWT/M2M secrets resolve from Vault
    - `IdentityClient:M2MClients` metadata (`Name`,`Description`,`Scopes`) resolves from Consul hierarchy
    - the `woosync-service` client metadata remains available as a non-sensitive baseline for the Corevia Sync WooCommerce provider
    - `M2M_WOOSYNC_SERVICE_SECRET` resolves from Vault and seeds the exact `woosync-service` client id used by Corevia Sync
    - DidarSync M2M client metadata and secret (`didarsync-service` / `M2M_DIDARSYNC_SERVICE_SECRET`) resolve correctly
    - Invoice/POS M2M metadata and secrets resolve correctly, with `pos.payment.start` assigned to `invoice-service` and `invoice.payment.confirm` assigned to `pos-service`
    - startup fails when either required Invoice/POS M2M secret or scope is unavailable

## Scenario 3: Discovery And Bootstrap
- Given:
	- Consul discovery registration exists for `identity-service` with canonical operational identifiers:
	- `ServiceID=nikplus-prd-online-shop-identity-service-5270`
	- `CheckID=service:nikplus-prd-online-shop-identity-service-5270`
- When:
	- the service starts and `/health` remains healthy
- Then:
	- discovery entry is healthy and points to a routable instance
	- bootstrap order remains `base sources -> AddConfigLoaderExtension(...) -> options/dependent registrations`

## Scenario 4: Fallback Runability
- Given:
	- Consul/Vault provider connectivity is temporarily unavailable
- When:
	- service starts with local bootstrap env and non-sensitive appsettings defaults
- Then:
	- service startup remains successful for baseline API hosting
	- warning logs indicate provider fallback activation without leaking secrets

## Scenario 5: OTP Discovery Consumer Fallback
- Given:
	- `ServiceDiscovery:Services:Otp` is configured and `OTP_BASE_URL` is present in environment
- When:
	- discovery provider is unavailable or returns no healthy OTP endpoint
- Then:
	- IdentityService uses `OTP_BASE_URL` fallback for OTP calls
	- service remains operational and logs discovery fallback warning
	- `OtpRestClient` does not override the BaseAddress selected during HttpClient registration
	- downstream OTP timeout returns a controlled upstream timeout before frontend callers abort

## Scenario 6: Explicit OTP Queue Acceptance
- Given:
	- OTPService stores the code and accepts the notification for background dispatch
- When:
	- `POST /v1/api/Identity/Send` completes
- Then:
	- Identity returns `202 Accepted` with `accepted=true` and `delivery=queued`
	- the client can enter the code-entry state without waiting for the SMS provider
	- a client rejects a missing or unexpected response payload instead of treating an arbitrary 2xx response as success

## Scenario 7: PaymentService Customer Balance Scope
- Given:
	- `payment-service` is configured as an M2M client
	- wallet top-up or checkout post-payment orchestration needs customer balance for notification parameters
- When:
	- PaymentService requests a token and calls `CustomerService` `GET /v1/api/Customer/ByPhone/{phoneNumber}?fresh=true`
- Then:
	- the issued token includes `customer.read`
	- CustomerService authorizes the request with `CustomerReadAccess`
	- receipt creation remains complete and notification balance lookup does not fail with `403 Forbidden`

## Scenario 6: Database Provider Adapter
- Given:
	- `IdentityDb:Provider` or `DB_PROVIDER` is unset
- When:
	- IdentityService starts
- Then:
	- the existing Postgres provider path is used
	- existing migration and seed behavior remains unchanged
- Given:
	- `IdentityDb:Provider` or `DB_PROVIDER` is set to `SqlServer`
- When:
	- SQL Server connection secrets resolve from Vault/environment
- Then:
	- IdentityService uses the SQL Server EF Core adapter
	- identity seed behavior remains unchanged
	- an existing SQL Server database is upgraded with required additive Identity columns before token/introspection queries run
	- the upgrade is idempotent and does not delete or rewrite existing identity data

## Scenario 7: MCP Sibling Boundary

- Given:
	- `IdentityService.Api` and `IdentityService.Mcp` are composed over the same Application/Domain layers
- When:
	- an MCP tool is invoked
- Then:
	- the MCP adapter calls the Application use case/MediatR handler directly
	- no MCP code calls an API controller, Identity HTTP endpoint, Gateway/Ocelot route, EF Core, or repository directly
	- Application and Domain assemblies have no dependency on either transport adapter

## Scenario 8: MCP Self Authorization

- Given:
	- a customer MCP client has `identity.mcp.self.read` or `identity.mcp.self.write`
- When:
	- the customer invokes a self tool with a `userId`, `tenantId`, or arbitrary `subjectId` for another identity
- Then:
	- the request is rejected
	- the effective Subject remains the current authenticated user
	- no cross-user or cross-tenant data is returned or changed
	- raw tokens, secrets, OTP codes, signing material, and database credentials are never present in the tool result

## Scenario 9: MCP Admin Authorization And Tenant Boundary

- Given:
	- an internal admin Agent has a validated authentication context and `identity.mcp.admin.read` or `identity.mcp.admin.write`
- When:
	- the Agent requests a User/Tenant/Session subject
- Then:
	- Actor is derived from the validated authentication context, not from prompt text or arbitrary metadata
	- the server resolves and authorizes the Subject against the Actor's tenant and effective permissions
	- missing or ambiguous Tenant context is denied rather than guessed
	- sensitive writes require approval bound to the exact tool/action/resource

## Scenario 10: MCP Delegated Execution Context

- Given:
	- an Agent is acting on behalf of a customer
- When:
	- a delegated MCP operation is invoked
- Then:
	- the Subject is resolved only from a short-lived, signed, audience-bound delegation context
	- the context is bounded by expiry, audience, tenant, allowed tools/scopes, anti-replay/nonce data, and verified membership of the delegated Subject in the delegated Tenant
	- a caller-supplied `subjectId` alone cannot authorize the operation
	- delegation cannot broaden the customer's effective permissions

## Scenario 11: MCP Tool Contract

- Given:
	- an Identity MCP tool is active
- When:
	- its schema is published to an MCP client
- Then:
	- the tool declares `kind` (`atomic` or `business`), `audience`, `risk`, `approvalRequired`, and `requiredScopes`
	- atomic tools expose bounded capabilities close to Application use cases
	- business tools expose a user/agent goal and may coordinate multiple Application use cases
	- both tool types reuse Application handlers and Domain rules
	- generic `call_identity_api`, arbitrary endpoint, SQL, and repository tools are unavailable

## Scenario 12: MCP Audit And Redaction

- Given:
	- any MCP tool call is accepted, denied, approved, or fails
- When:
	- the execution record is written
- Then:
	- the record includes Actor, Subject, Tenant, Mode, tool name/version, required/effective scopes, authorization decision, approval id/status, result status, and correlation id
	- raw tokens, secrets, OTP codes, signing keys, and unredacted sensitive payloads are not recorded

## Scenario 13: MCP Local Installation And Cloud Client

- Given:
	- `IdentityService.Mcp` is installed on a customer's local machine without Internet access
- When:
	- a local MCP client invokes it
- Then:
	- `stdio` works without a central gateway or Internet
	- if loopback HTTP is enabled, it binds only to `127.0.0.1`/`::1`, keeps Authentication/Authorization enabled, and is not publicly forwarded

- Given:
	- a cloud-hosted AI needs to use the local installation
- When:
	- a trusted local MCP client/desktop connector relays a tool call
- Then:
	- the cloud AI reaches MCP only through that local connector
	- an offline MCP is not directly reachable from the cloud
	- relayed data follows customer consent, redaction, and data-egress policy

## Scenario 14: MCP Customer Server Installation

- Given:
	- `IdentityService.Mcp` is installed on a customer's server
- When:
	- a remote MCP client connects
- Then:
	- public access uses authenticated HTTPS, or private HTTP is limited to a trusted private network with equivalent controls
	- the installation works without a Corevia central gateway, public inbound tunnel, or Internet dependency

## Scenario 15: Future Enterprise Federation Boundary

- Given:
	- enterprise authentication is enabled in a future deployment
- When:
	- an operator signs in through the enterprise identity path
- Then:
	- the supported path may be `Active Directory (AD/LDAP) -> Keycloak User Federation -> OIDC/OAuth2 -> Identity MCP / future MCP Gateway`
	- MCP does not bind directly to AD/LDAP
	- Keycloak/AD and the central MCP Gateway remain optional and are not required for local/customer-server modes

## Scenario 16: Corevia MCP Manifest And Standard Validator

- Given:
	- the Identity MCP manifest is present under `.corevia/mcp/identity-service.yaml`
- When:
	- `corevia-run validate --pattern mcp-contract.validate --values .corevia/operations/identity-service-mcp-contract.values.yaml` runs
- Then:
	- the pinned Corevia Standard, Application boundary, modes, scopes, Tool metadata, transports, audit, redaction, rollback, and generic A01–A16 mapping pass
	- a manifest pass is treated as contract evidence only; runtime implementation and security tests remain required

## Executed MCP Runtime Evidence

The first executable Identity MCP baseline is implemented and locally verified:

- `IdentityService.Mcp` is a sibling adapter and invokes Application/MediatR directly; no API/controller/HTTP mirror, EF Core, repository, SQL, or raw provider path is used by MCP code.
- The active allowlist contains exactly 36 tools: 34 Atomic capability tools and 2 Business goal tools. Five API protocol operations remain explicitly catalogued as deprecated, non-exposable inventory bindings because raw token/secret exposure is forbidden.
- The API/MCP permission parity catalog contains 41 one-to-one mappings: 36 active authorization permissions have usable active MCP Tools, while the five prohibited protocol operations have explicit non-exposable bindings. API and MCP grants remain separate, and any new API authorization permission, orphan mapping/tool, or restriction drift fails closed in the Standard validator and CI.

### Roadmap closure gates

- Identity MCP acceptance is not closed by local code, local tests, or a valid manifest alone. The Standards, Bridge, and Market changes must be committed, pushed, merged into their declared target branches, and verified by green remote CI.
- Phase 7 must record remote/release evidence and a real MCP smoke for the selected local or customer-server transport. Phase 8 customer pilot, package publication, production deployment, and external provider changes remain separate approved gates.
- The reusable future-service executor is a tracked follow-up. Until an approved child `apply` executor exists, the generic facade remains read-only for mutation and `extension-needed` is the required result; manual mutation is never acceptance evidence.
- The server owns Actor/Subject/Tenant context and enforces Self/Admin/Delegated rules, separate scopes, tenant boundaries, signed bounded delegation, anti-replay nonces, exact approval, redaction, and audit.
- The MCP project builds successfully with zero warnings and zero errors; `IdentityService.Mcp.Tests` passes its complete catalog/security suite.
- The stdio protocol smoke completes `initialize` and `tools/list`, publishes exactly 36 active tools, keeps stdout protocol-only, and runs without a Kestrel listener.
- HTTP transport is opt-in, authenticated, Host/Origin guarded, loopback-safe by default, and requires HTTPS or explicit trusted private-network controls for non-loopback operation; isolated HTTPS/private-network profile smokes passed.
- Remote CI/MR verification, customer-server pilot, package publication, production deployment, and future Gateway/federation work remain pending release gates; these acceptance scenarios must not be marked complete from local evidence alone.

Detailed commands and redacted results are recorded in [MCP Runtime Implementation Evidence](./mcp-runtime-implementation-evidence.md).

## Negative Scenarios
- Missing required config:
	- startup/deploy is rejected when provider bootstrap values are absent
- Missing required secret:
	- startup fails due to missing `JWT_SECRET` or credentials for the selected database provider
- Provider `401/403`:
	- migration is blocked until runtime token/policy is corrected
- Invalid path/mount/prefix:
	- migration is blocked until Consul/Vault prefix/path values are corrected
- Discovery registration failure:
	- migration is blocked until registration and health check are consistent
- Bootstrap order violation:
	- migration is blocked when `AddConfigLoaderExtension(...)` is moved after options binding
- MCP calls an API controller or HTTP endpoint:
	- architecture validation fails
- MCP receives Actor/Subject only from prompt text or arbitrary metadata:
	- authorization is denied
- MCP delegated context is expired, unsigned, wrong-audience, over-broad, or replayed:
	- authorization is denied
- MCP self tool receives a cross-user/cross-tenant selector:
	- authorization is denied
- Sensitive MCP mutation has no exact approval:
	- execution is blocked
- Local MCP HTTP binds to a public interface or is exposed through unauthenticated forwarding:
	- deployment validation fails
- MCP exposes raw token/secret/OTP data:
	- tool contract and security validation fail

## Observability
- Required logs from shared `Logging`:
	- startup and request-level logs are available for operator diagnosis
- Required error shape/exception path from shared `ErrorHandling`:
	- API errors are shaped through shared middleware
- Required operator-facing human-readable messages:
	- deployment/runtime failures identify missing provider variable names without leaking secrets

## Final Review Gate
- Local files cleaned after migration: Yes
- `appsettings.json` keeps non-sensitive fallback defaults only: Yes
- Real runtime tokens/credentials validated: Yes
- `IConfiguration` injection validated: Yes
- At least one config/secret-dependent endpoint passes smoke test: Yes (`POST /v1/api/Identity/Send`)
- Discovery registration and health endpoint validated: Yes
- Consul service/check canonical ids validated on runtime host: Yes

## Last Updated
- 2026-07-06

- 2026-09-03
