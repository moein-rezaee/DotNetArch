[فارسی](./contracts.fa.md)

# IdentityService Runtime Migration Contracts

> Status: Active implementation baseline; controlled pilot pending
> Version: 0.11.1
> Owner: IdentityService

## Customer Session Contract
- OTP customer login issues a refresh token without an expiry timestamp.
- The refresh token is rotated on refresh and remains valid until logout/revocation.
- Existing refresh tokens that already have an expiry continue to honor that expiry.
- The `Refresh` response remains `{ accessToken, refreshToken }`; no client contract change is required.

## OTP Send Contract
- `POST /v1/api/Identity/Send` accepts the normalized phone number and returns `202 Accepted` with `{ "accepted": true, "delivery": "queued" }` after OTPService accepts the notification for background dispatch.
- `delivery: "queued"` means the request was accepted for NotificationService delivery; it does not claim that the SMS provider or handset has completed delivery.
- Clients must validate `accepted` and `delivery` before moving to the code-entry state instead of treating an arbitrary successful HTTP status as a valid send response.

## Corevia MCP Standard Conformance

The generic MCP contract is owned by the [Corevia MCP Source Standard](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/contracts.md), its [source manifest schema](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/schemas/mcp/mcp-source-manifest.schema.yaml), and the [Identity MCP manifest](../../.corevia/mcp/identity-service.yaml). Source creation follows the [generic source Pattern values](../../.corevia/operations/identity-service-mcp-source.values.yaml). This service contract intentionally keeps only Identity-specific semantics and references the generic Standard instead of copying it.

### API/MCP permission parity

The Identity API capability catalog and the MCP manifest are one governed surface with separate authorization namespaces and grants. The catalog contains 41 entries: 36 active authorization permissions must each have a usable active MCP Tool with the same action, resource, audience, risk, approval, tenant scope, and server-owned subject restrictions. Five protocol operations are explicitly classified as `mcpExposure: prohibited` and remain inventory-only because raw token or client-secret exposure is forbidden. The Corevia Standard and CI fail closed on a new API permission without a mapping, an orphan or unusable Tool, restriction drift, or an exposed prohibited operation.

## Identity MCP Adapter Boundary Contract

- `IdentityService.Mcp` is a sibling adapter to `IdentityService.Api` and calls Application use cases/MediatR handlers directly.
- Controllers, the Identity HTTP API, Gateway/Ocelot routes, EF Core, repositories, SQL, and raw infrastructure providers are forbidden dependencies.
- Application and Domain have no dependency on either transport adapter.
- Local/customer-server installations preserve the same capability, authorization, approval, audit, and redaction semantics; only transport/deployment changes.
- A central Corevia MCP Gateway is optional future infrastructure and is not required for current installations.

## Identity Authentication, Authorization, and Context Contract

Identity MCP uses scopes distinct from API scopes:

- `identity.mcp.self.read`
- `identity.mcp.self.write`
- `identity.mcp.admin.read`
- `identity.mcp.admin.write`
- `identity.mcp.security.read`
- `identity.mcp.security.write`

The server creates `McpExecutionContext` for every call with Actor, Subject, Tenant, `Mode` (`self`, `admin`, or `security`), optional signed delegation context, Tool name/version, required/effective scopes, risk, approval state, and CorrelationId.

- `self`: current authenticated customer only; `userId`, `tenantId`, and arbitrary `subjectId` cannot select another identity.
- `admin`: internal human/service/Agent context; a structured User/Tenant/Session Subject is accepted only after server-side Tenant and Permission authorization.
- `security`: separate Client/Scope/Permission/Secret metadata capability, disabled by default in the MVP.
- Tenant context: Admin requires exactly one validated `tenant_id` claim. User access/refresh tokens add it only for one membership or one default membership; an M2M token can receive it only from the explicit `M2MClientConfig.TenantId` binding. Missing or ambiguous tenant context is denied.
- Delegation: signed, short-lived, audience-bound, tenant-bound, Tool/Scope-limited, expiry-checked, anti-replay/nonce-protected, and subject-membership-checked against the delegated tenant. It is not a fourth Mode or an escalation.

Two Agent audiences are supported: an internal administrative Agent with `identity.mcp.admin.*`, and a customer-authorized Agent with the current `identity.mcp.self.*` capability plus a valid delegated context when required. Prompt text, arbitrary metadata, and natural-language confirmation never grant authorization.

Sensitive mutations require exact approval bound to Tool/Action/Resource/Tenant/Actor. Audit records include Actor, Subject, Tenant, Mode, Tool name/version, required/effective scopes, Authorization decision, Approval id/status, result status, and CorrelationId; raw tokens, Secrets, OTP, signing material, database credentials, and unredacted sensitive payloads are forbidden.

## Identity Tool Contract

Identity exposes both:

- **Atomic:** one bounded Application capability;
- **Business:** one user/Agent goal coordinating multiple Application use cases.

Every Tool declares versioned `kind`, `audience`, `risk`, `approvalRequired`, `requiredScopes`, status, exposure state, Application entry point, and redacted output. Business Tools may orchestrate handlers but must not duplicate Domain rules. Generic `call_identity_api`, arbitrary endpoint/SQL/Repository Tools and raw sensitive output are forbidden. Security Tools are not exposed by default in the MVP.

### Executable security rules

- The server creates a fresh `McpExecutionContext` for every call. `Actor`, `Subject`, `Tenant`, `Mode`, delegation, tool metadata, required/effective scopes, approval state, and correlation id are server-owned values.
- `Actor` and `Subject` are never accepted from prompt text, arbitrary metadata, or a caller-supplied `subjectId`. `Actor` comes from validated authentication. The server resolves the target subject and tenant before Application execution.
- In `self` mode, `actor = subject`; the tool input must not contain `userId`, `tenantId`, `subjectId`, or `actor`, and the operation cannot cross users or tenants. Delegation may replace the subject only through a valid bounded signed context.
- In `admin` mode, the authenticated Actor must have the required admin MCP scope and a single authorized tenant context. The target User/Tenant/Session is resolved server-side and must remain within that tenant. Missing or ambiguous tenant context is denied; admin does not accept delegated mode.
- `security` scopes are separate and disabled by default. Client, Scope, Permission, and Secret management is not part of the current exposed catalog.
- A delegated context is signed, short-lived (at most five minutes), audience-bound to `identity-service-mcp`, tenant-bound, nonce-protected, limited to an allowlist of Tools and Scopes, and accepted only when the delegated Subject is a member of the delegated Tenant according to the Identity Application. Effective scopes are the intersection of authenticated and delegated scopes; delegation cannot escalate.
- `identity_revoke_user_sessions` requires an exact signed approval bound to actor, tenant, audience, tool/version, action, resource, expiry, and nonce. The all-other-sessions resource also binds the authenticated current session when one applies. Conversational “yes” is not approval.
- Every accepted, denied, failed, or cancelled call is audited with Actor, Subject, Tenant, Mode, Tool name/version, required/effective scopes, authorization decision, approval id/status, result status, and correlation id. Raw tokens, Secrets, OTP, signing material, database credentials, and unredacted payloads are never returned or audited.

The executable Identity catalog is versioned and complete for the current API capability set: 36 active Tools (34 Atomic and 2 Business) cover profile, session, user, role, tenant, permission, scope, client, and relationship capabilities. The full mapping and input restrictions are authoritative in [the machine-readable Identity MCP manifest](../../.corevia/mcp/identity-service.yaml); five protocol-operation descriptors remain non-exposable inventory entries.

Representative executable Tools are:

| Tool | Kind | Audience | Risk | Scope | Application boundary |
|---|---|---|---|---|---|
| `identity_get_self_profile` | atomic | self | read | `identity.mcp.self.read` | `GetProfileQuery` |
| `identity_list_self_sessions` | atomic | self | read | `identity.mcp.self.read` | `GetCurrentUserSessionsQuery` |
| `identity_get_user_access_summary` | business | admin | read | `identity.mcp.admin.read` | `GetUserByIdQuery`, `GetUserRolesQuery`, `GetUserTenantsQuery` |
| `identity_revoke_user_sessions` | business | admin | sensitive | `identity.mcp.admin.write` | `RevokeSessionCommand` or `RevokeOtherSessionsCommand` |

## Identity Deployment and Federation Contract

- Local customer: authenticated HTTP (the default transport) binds only to `127.0.0.1`/`::1`, validates Host/Origin where applicable, and has no public DNS/NAT/port-forward/unauthenticated reverse proxy.
- Customer server: authenticated HTTPS, or private HTTP in a trusted network with equivalent controls; no central Gateway, public inbound tunnel, or Internet dependency is required.
- Cloud AI: local MCP is reachable only through a trusted local MCP Client/Desktop Connector with customer consent, redaction, and egress policy.
- Future enterprise path: `Active Directory (AD/LDAP) -> Keycloak User Federation -> OIDC/OAuth2 -> Identity MCP / optional future MCP Gateway`; MCP never binds directly to AD/LDAP.

Runtime configuration contract:

- `MCP_TRANSPORT=http|stdio`, defaulting to `http`. `stdio` is a development-only process transport and the host refuses to start with it outside the Development environment (`mcp_stdio_dev_only`).
- `COREVIA_MCP_BEARER_TOKEN` is accepted only for stdio process authentication; HTTP never falls back to a process token when the request is anonymous.
- `MCP_HTTP_BIND_ADDRESS` must be a numeric address. Loopback is the default; non-loopback public access requires HTTPS, while private HTTP additionally requires explicit `MCP_PRIVATE_NETWORK_TRUSTED=true`.
- `MCP_HTTP_PORT`, `MCP_HTTP_HTTPS_ENABLED`, `MCP_ALLOWED_HOSTS`, and `MCP_ALLOWED_ORIGINS` control the guarded HTTP profile.
- `MCP_DELEGATION_SIGNING_KEY` and `MCP_APPROVAL_SIGNING_KEY` are runtime secrets supplied by Vault/environment only and are absent from source configuration.
- For HTTP, `/health` is anonymous and `/mcp` is authenticated and Host/Origin checked. For stdio, stdout contains only MCP protocol frames and logs go to stderr. The stdio host does not create a Kestrel listener.

## Local Audit Contract
- `appsettings.json` reviewed: Yes (non-sensitive fallback defaults retained for `Jwt`, `IdentityClient`, `Swagger`)
- real `.env` reviewed: Yes (legacy local secrets detected and removed)
- `docker-compose.yml` reviewed: Yes (env file + mounted runtime artifacts validated)
- `Program.cs` reviewed: Yes (bootstrap order aligned)
- option classes reviewed: Yes (`JwtOptions`, `RootAdminOptions`, `IdentityClientOptions`)
- unused/duplicate local keys removed: Yes

## Classification Contract
- Consul-managed config keys:
  - `identity-db/provider`
  - `identity-db/command-timeout-seconds`
  - `jwt/issuer`
  - `jwt/audience`
  - `jwt/access-token-minutes`
  - `jwt/refresh-token-days`
  - `swagger/enabled`
  - `swagger/documents/0/{name,title,version,description}`
  - `swagger/security/enable-bearer`
  - `swagger/security/description`
  - `swagger/ui/{enable,route-prefix,expose-in-production,display-request-duration,enable-filter,default-models-expand-depth}`
  - `identity-client/default-public-client-id`
  - `identity-client/m2m-clients/<client-id>/name`
  - `identity-client/m2m-clients/<client-id>/description`
  - `identity-client/m2m-clients/<client-id>/scopes/<index>`
- Vault-managed secret keys:
  - `POSTGRES_HOST`
  - `POSTGRES_PORT`
  - `IDENTITY_POSTGRES_DB`
  - `POSTGRES_USER`
  - `POSTGRES_PASSWORD` (required, no default; a missing value fails with an `InvalidOperationException` naming the key)
  - `SQLSERVER_HOST`
  - `SQLSERVER_PORT`
  - `IDENTITY_SQLSERVER_DB`
  - `SQLSERVER_DB`
  - `SQLSERVER_USER`
  - `SQLSERVER_PASSWORD` (no default; required only when `SQLSERVER_USER` is set, in which case a missing value fails with an `InvalidOperationException` naming the key; with neither user nor password, Integrated Security is used)
  - `MSSQL_SA_PASSWORD`
  - `JWT_SECRET`
  - `IDENTITY_ROOT_PHONE`
  - `M2M_IDENTITY_SERVICE_SECRET`
  - `M2M_OTP_SERVICE_SECRET`
  - `M2M_ORDER_SERVICE_SECRET`
  - `M2M_INVOICE_SERVICE_SECRET`
  - `M2M_POS_SERVICE_SECRET`
  - `M2M_CATALOG_SERVICE_SECRET`
  - `M2M_DIDARSYNC_SERVICE_SECRET`
  - `M2M_CUSTOMER_SERVICE_SECRET`
  - `M2M_BASKET_SERVICE_SECRET`
  - `M2M_PAYMENT_SERVICE_SECRET`
  - `M2M_WOOCOMMERCE_SERVICE_SECRET`
  - `M2M_WOOSYNC_SERVICE_SECRET`
  - `M2M_NOPCOMMERCE_SERVICE_SECRET`
- The `woosync-service` client is the service-auth contract used by the WooCommerce provider in the independent `corevia-sync` repository. Its metadata and scopes are non-sensitive IdentityClient defaults; its secret is loaded from Vault and must never be committed.
- Identity seeds `pos.payment.start` for `invoice-service` and `invoice.payment.confirm` only for `pos-service`.
- The financial clients declare `M2M_INVOICE_SERVICE_SECRET` and `M2M_POS_SERVICE_SECRET` as required seed inputs. Identity startup fails instead of silently omitting either client when its Vault-backed secret or required scope is missing.
- Consul-managed discovery items:
  - service registration name: `identity-service`
  - service registration id: `nikplus-prd-online-shop-identity-service-5270`
  - service check id: `service:nikplus-prd-online-shop-identity-service-5270`
  - service health path: `/health` (liveness, cheap, unchanged payload)
  - readiness path: `/health/ready` (EF Core can-connect on the database, 3 s timeout; `200` when reachable, `503` when not). The Mcp HTTP host exposes the same two paths anonymously.
  - service port metadata: `5270`
  - consumer discovery config for OTP dependency:
    - `service-discovery/services/otp/service-name`
    - `service-discovery/services/otp/service-names`
    - `service-discovery/services/otp/scheme`
- local bootstrap-only keys retained:
  - `CONSUL_HTTP_ADDR`
  - `CONSUL_HTTP_TOKEN`
  - `CONFIG_CENTER_PREFIX`
  - `VAULT_ADDR`
  - `VAULT_TOKEN`
  - `SECRET_STORE_PATH`
  - `IDENTITY_APP_VERSION`
  - `OTP_BASE_URL`
  - `OTP_HTTP_TIMEOUT_SECONDS`

## Discovery Consumer Fallback Contract
- IdentityService resolves OTP dependency endpoint primarily from `ServiceDiscovery:Services:Otp`.
- If discovery provider is unavailable or no healthy endpoint is found, service must fallback to `OTP_BASE_URL` from environment-loaded `IConfiguration`.
- Discovery failure must be observable via warning logs and must not crash baseline startup when fallback key exists.
- OTP endpoint selection happens only during HttpClient registration. `OtpRestClient` (Corevia.Kit.Http named provider `IdentityOtp`) must use the configured `HttpClient.BaseAddress` and must not re-read `OTP_BASE_URL`.
- OTP request timeout is controlled by `OTP_HTTP_TIMEOUT_SECONDS` or `IdentityOtp:TimeoutSeconds`; invalid or missing values use the 8 second default.
- Downstream OTP timeout must map to a controlled upstream timeout response instead of leaking a raw task cancellation or leaving frontend callers without a response.

## Fallback Contract
- Local fallback defaults in `appsettings.json` are intentionally retained for non-sensitive settings to keep service startup resilient when centralized providers are temporarily unavailable.
- Consul values take precedence when provider access is healthy; fallback values are not the primary production source.
- Database provider selection defaults to Postgres when neither `IdentityDb:Provider` nor `DB_PROVIDER` is configured, preserving the existing production behavior.
- SQL Server is enabled only when the provider value is `SqlServer` or `Sql`; SQL Server credentials must resolve from Vault/environment before startup continues.

## Naming Contract
- Consul prefix:
  - `nikplus/online-shop/prd/identity-service`
- Consul relative key examples:
  - `jwt/issuer`
  - `swagger/ui/route-prefix`
  - `identity-client/default-public-client-id`
  - `identity-client/m2m-clients/order-service/scopes/0`
- Vault secret path:
  - `nikplus/online-shop/prd/identity-service`
- Vault secret key examples:
  - `JWT_SECRET`
  - `POSTGRES_PASSWORD`
  - `SQLSERVER_PASSWORD`
  - `M2M_PAYMENT_SERVICE_SECRET`
- Discovery service name / health path:
  - `identity-service` / `/health`
- Discovery operational identifiers:
  - `ServiceID=nikplus-prd-online-shop-identity-service-5270`
  - `CheckID=service:nikplus-prd-online-shop-identity-service-5270`

## Execution Order Contract
- Local cleanup is completed before centralized provisioning:
  - Yes
- Consul config, Vault secrets, and discovery registration are created/updated before any CI/CD change:
  - Yes
- Provider readability and discovery health are verified before CI/CD change:
  - Yes
- CI/CD edits are blocked until centralized provisioning and readability/health checks pass:
  - Required and enforced by governance/checklist
- CI variable contract for provider bootstrap:
  - Shared generic keys are used: `CONSUL_HTTP_ADDR`, `CONSUL_HTTP_TOKEN`, `CONFIG_CENTER_PREFIX`, `VAULT_ADDR`, `VAULT_TOKEN`, `SECRET_STORE_PATH`
  - Service-specific keys are removed from CI contract
  - Deploy job derives effective prefix/path as `<base>/identity-service`

## Bootstrap Order Contract
- Base configuration sources are added before `AddConfigLoaderExtension(...)`: Yes (`appsettings.json` + environment variables)
- `AddConfigLoaderExtension(...)` executes before options binding: Yes
- `AddConfigLoaderExtension(...)` executes before config-dependent service registration: Yes
- Services/extensions that depend on centralized values:
  - `AddConfigCenterExtension(...)`
  - `AddSecretStoreExtension(...)`
  - `AddJwtSecurityExtension(...)`
  - `AddIdentityInfrastructure(...)`
  - `AddOptions<JwtOptions>(...)`
  - `AddOptions<RootAdminOptions>(...)`
  - `AddOptions<IdentityClientOptions>(...)`

## Failure Handling Contract
- Missing required config behavior:
  - startup/deploy is rejected when provider bootstrap keys are missing
- Missing required secret behavior:
  - startup fails if `JWT_SECRET` or required credentials for the selected database provider are unresolved
- `401/403` provider access behavior:
  - migration/deploy is blocked until runtime token/policy is corrected
- Invalid path/mount/prefix behavior:
  - bootstrap path/prefix must be corrected in runtime env
- Discovery registration failure behavior:
  - migration is incomplete until Consul registration and `/health` are healthy
- Shared logging/error-handling paths used:
  - `Logging` and `ErrorHandling` are wired in API pipeline

## Verification Contract
- Provider readability verified with runtime credentials/tokens: Yes
- Injected keys verified in `IConfiguration`: Yes
- Discovery registration verified in Consul: Yes
- Discovery operational identifiers verified in Consul: Yes (`ServiceID` and `CheckID`)
- M2M metadata hierarchy verified in Consul: Yes (`identity-client/m2m-clients/*`)
- Smoke-tested endpoint(s):
  - `/health`
  - `GET /`
  - `GET /swagger/index.html`
  - `POST /v1/api/Identity/Send`

## Last Updated
- 2026-06-12

- 2026-09-03
