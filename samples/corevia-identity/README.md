[فارسی](./README.fa.md)

# IdentityService (OnlineShop)


## Overview

IdentityService provides OTP-based login, JWT access/refresh tokens, basic user storage, and role/permission/scope/client/session/tenant management for the OnlineShop stack.

- OTP login via dedicated OTPService
- JWT Bearer authentication for other services
- EF Core persistence through a database provider adapter. Postgres remains the default; SQL Server can be selected through runtime configuration.
- Clean separation into Api / Application / Domain / Infrastructure projects

## Architecture & Stack

- Target Framework: `.NET 9`
- Projects:
  - `IdentityService.Api` – ASP.NET Core Web API (controllers, Program, Dockerfile)
  - `IdentityService.Application` – CQRS (MediatR), validators, services
  - `IdentityService.Domain` – entities + interfaces (`User`, `UserProfile`, `Role`, `Permission`, `Scope`, `Client`, `Tenant`, `UserSession`, join tables, etc.)
  - `IdentityService.Infrastructure` – OTP REST client (`OtpRestClient`, on `Corevia.Kit.Http`) and infrastructure wiring (delegates database registration to `IdentityService.Infrastructure.Database`)
  - `IdentityService.Infrastructure.Database` – `IdentityDbContext`, EF Core repositories/unit of work, `DbInitializer`, and migrations for Identity's own storage. Provider selection and connection-string/EF-provider wiring (Postgres default, SQL Server alternate via `IdentityDb:Provider`/`DB_PROVIDER`) are delegated to `Corevia.Kit.DatabaseConnection`'s single `AddCoreviaDatabase<TContext>` entry point — this project no longer branches on engine itself and replaces the former two-project-per-engine `IdentityService.Infrastructure.Postgres`/`.Infrastructure.SqlServer` split.
  - `IdentityService.Mcp` – implemented sibling MCP adapter; calls Application use cases directly and does not call the API over HTTP
- Libraries:
  - MediatR, FluentValidation
  - `Corevia.Kit.*` (Logging, ErrorHandling, Swagger, JwtSecurity, Config, Secrets, ConfigLoader incl. `.env` loading, ServiceDiscovery, Http, DatabaseConnection). The legacy `shared/*` projects are no longer referenced.
  - Kit consumption is via published Kit packages on Nexus: every `Corevia.Kit.*` dependency is a versioned `PackageReference` restored from the Nexus NuGet feed (see `NuGet.config`); no sibling `corevia-kit` checkout or `Directory.Build.props` stopgap is required.
  - Build: `dotnet build IdentityService.sln`; tests: `dotnet test IdentityService.sln`.

## Authentication & Tokens

- Login flow:
  - `POST /v1/api/Identity/Send` → accepts OTP delivery through OTPService and returns `202` when queued
  - `POST /v1/api/Identity/Verify` → validates OTP, finds/creates `User`, opens `UserSession`, issues:
    - Access token (JWT, HS256) – claims: `sub` (User.Id), `phone`, `jti`, `sid` (session id), and an unambiguous `tenant_id` when the user has one membership or one default membership
    - Refresh token – stored in `RefreshToken` table with `UserSessionId`
- Refresh:
  - `POST /v1/api/Identity/Refresh` – validates refresh token, rotates it, issues new access + refresh tokens
- Logout:
  - `POST /v1/api/Identity/Logout` – revokes the given refresh token and, if bound to a session, marks that `UserSession` as revoked

Token configuration (`appsettings.json` → `Jwt` section):
- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:AccessTokenMinutes`
- `Jwt:RefreshTokenDays`
Secret key:
- `JWT_SECRET` (env only; at least 16 bytes for HS256)

## Main Endpoints (API Surface)

All routes are under `/v1/api/...` and use PascalCase segments after the prefix.

### Identity (OTP + Tokens)

- `POST /v1/api/Identity/Send`
- `POST /v1/api/Identity/Verify`
- `POST /v1/api/Identity/Refresh`
- `POST /v1/api/Identity/Logout`

### Profile (Current User)

- `GET /v1/api/Profile`
  - Returns `UserProfileResponse`:
    - `Id`, `PhoneNumber`, `CreatedAt`, `UpdatedAt`
    - `FirstName`, `LastName`, `Email`, `AvatarUrl`
- `PUT /v1/api/Profile`
  - Body: `UpdateProfileRequest` with optional `FirstName`, `LastName`, `Email`, `AvatarUrl`
  - Uses `sub` claim from JWT to find `User` and manages `UserProfile` (separate entity)

### Users

- `GET /v1/api/Users`
  - Query: `pageNumber`, `pageSize`, `phone?`, `tenantId?`, `roleId?`
  - Response: `PagedResponse<UserListItemDto>` with `Id`, `PhoneNumber`, `CreatedAt`, `UpdatedAt`, `IsActive`
- `GET /v1/api/Users/{id}`
  - Response: `UserDetailDto` with `Id`, `PhoneNumber`, `CreatedAt`, `UpdatedAt`, `IsActive`, nested `Roles[]` and `Tenants[]`
- `POST /v1/api/Users`
  - Body: `CreateUserRequest` (`PhoneNumber`, `IsActive`)
- `PUT /v1/api/Users/{id}`
  - Body: `UpdateUserRequest` (`PhoneNumber`, `IsActive`)
- `DELETE /v1/api/Users/{id}`
  - Soft delete (sets `IsActive=false`)

### UserTenants & UserRoles

- `UserTenantsController` – `/v1/api/Users/{userId}/Tenants`
  - `GET` – list tenant mappings with `IsDefault`
  - `POST` – add tenant (`UserTenantRequest(TenantId, IsDefault)`)
  - `DELETE /{tenantId}` – remove mapping

- `UserRolesController` – `/v1/api/Users/{userId}/Roles`
  - `GET` – list roles of a user
  - `POST` – add role (`UserRoleRequest(RoleId)`)
  - `DELETE /{roleId}` – remove role

- `TenantUsersController` – `/v1/api/Tenants/{tenantId}/Users`
  - `GET` – list users in a given tenant (internally uses the `tenantId` filter of `GET /Users`)

- `RoleUsersController` – `/v1/api/Roles/{roleId}/Users`
  - `GET` – list users in a given role (internally uses the `roleId` filter of `GET /Users`)

### Roles & Permissions

- Roles: `/v1/api/Roles`
  - `GET` (paged), `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}`
  - Roles include nested `Permissions[]` in details

- Permissions: `/v1/api/Permissions`
  - `GET` (paged)
  - `GET {id}`
  - `POST` – create permission (`Key`, `DisplayName`, `Description?`)
  - `PUT {id}` – update, including `IsDeprecated`, `DeprecationReason`
  - `DELETE {id}` – soft-delete (marks as deprecated)

### Scopes & Clients

- Scopes: `/v1/api/Scopes`
  - `GET` (paged), `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}`
  - Scope details include nested `Permissions[]`

- Clients: `/v1/api/Clients`
  - `GET` (paged), `GET {id}`
  - `POST` – create client (auto-generates `ClientId`) and assign scopes
  - `PUT {id}` – update client + scopes
  - `GET {id}/Secrets` – list secret metadata (no raw secret values)
  - `POST {id}/Secrets` – create a new secret, returns raw secret only once
  - `DELETE {id}/Secrets/{secretId}` – revoke secret

### Tenants

- `/v1/api/Tenants`
  - `GET` (paged), `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}`
  - Tenants expose `Id`, `Name`, `DisplayName`, `IsActive`, `ExternalId?`

### Sessions

- `/v1/api/Sessions` (current user)
  - `GET` – list sessions (`SessionDto(Id, CreatedAt, EndedAt, DeviceInfo, IpAddress, IsRevoked)`)
  - `DELETE /{id}` – revoke a specific session + its active refresh tokens
  - `DELETE` – revoke all sessions except current (`sid` from JWT)

### OAuth2 / OIDC-ish Utility Endpoints

> These endpoints provide a light OIDC/OAuth2 surface around existing JWT/refresh flows; they are designed for internal use and can evolve into a full identity provider if needed.

- Discovery:
  - `GET /.well-known/openid-configuration`
  - `GET /.well-known/jwks.json` (empty for symmetric keys)
- Token:
  - `POST /token` – currently supports `grant_type=refresh_token` (wraps `/v1/api/Identity/Refresh`)
- Revocation:
  - `POST /revoke` – revokes refresh tokens (wraps `/v1/api/Identity/Logout`)
- Introspection:
  - `POST /introspect` – returns `active` + basic claims for refresh tokens and access tokens
- UserInfo:
  - `GET /userinfo` – returns claims (`sub`, `phone`, `sid`) from access token

## MCP Surface (Corevia Standard Profile)

The generic architecture, security, ToolSpec, transport, migration, compatibility, rollback, and acceptance rules are inherited from the [Corevia MCP Source Standard](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/README.md) and the [normative MCP contracts](https://gitlab.getcorevia.ir/corevia/standards/-/blob/main/docs/specs/mcp/contracts.md). IdentityService is the first service profile consuming that standard; it does not redefine the generic rules locally.

Identity-specific source contract:

```text
IdentityService.Domain
        ↑
IdentityService.Application
        ↑                 ↑
IdentityService.Api   IdentityService.Mcp
```

- `IdentityService.Mcp` is a sibling adapter and calls Application use cases/MediatR handlers directly.
- Controllers, the Identity HTTP API, Gateway/Ocelot routes, EF Core, repositories, SQL, and raw infrastructure providers are not MCP dependencies.
- Identity MCP combines bounded **atomic** Tools and higher-level **business** Tools; Tool metadata is versioned and declared in the machine-readable [Identity MCP manifest](.corevia/mcp/identity-service.yaml).
- Identity scopes are `identity.mcp.self.read`, `identity.mcp.self.write`, `identity.mcp.admin.read`, `identity.mcp.admin.write`, `identity.mcp.security.read`, and `identity.mcp.security.write`.
- The Identity seed registers these scopes and grants only `identity.mcp.self.read` to the default public client. Admin scopes require explicit client/role assignment and a single tenant binding; an M2M admin client must configure `IdentityClient:M2MClients:<client-id>:TenantId`.
- The internal administrative Agent uses the `admin` profile; a customer-authorized Agent uses the current `self` capability and, where required, a signed short-lived delegated context. Delegation never broadens permission.
- Security operations are not exposed by default in the MVP. Generic API/endpoint/SQL/repository proxy Tools and raw token, Secret, OTP, signing-material, or database-credential output are forbidden.
- Current deployment is deployment-neutral: authenticated loopback HTTP by default (`stdio` is development-only), and authenticated HTTPS/private-network customer-server mode. A central Gateway is optional future infrastructure, not a current dependency.
- A cloud AI reaches local MCP only through a trusted local MCP client/Desktop Connector and customer-approved egress. MCP never binds directly to AD/LDAP; the future path is AD/LDAP → Keycloak User Federation → OIDC/OAuth2.

Use [the Identity MCP create-or-migrate values](.corevia/mcp/identity.values.yaml) as the single service-level entry point. It detects the current source state and routes to the governed create or migration Pattern chain. Validate the Identity profile with [the Corevia MCP contract values](.corevia/operations/identity-service-mcp-contract.values.yaml), route source creation through [the Corevia MCP source values](.corevia/operations/identity-service-mcp-source.values.yaml), and validate the active migration/adoption profile with [the Identity MCP migration values](.corevia/mcp/identity.values.yaml). The implementation roadmap remains [IdentityService MCP Adapter Roadmap](docs/roadmaps/services/identity-service-mcp-roadmap.md).

The implementation roadmap is [IdentityService MCP Adapter Roadmap](docs/roadmaps/services/identity-service-mcp-roadmap.md). It is the canonical execution plan for the sibling project, security policies, tool catalog, local/customer-server deployment, tests, and controlled pilot. The current runtime results are recorded in [MCP Runtime Implementation Evidence](docs/specs/mcp-runtime-implementation-evidence.md).

## Running the MCP Adapter

`IdentityService.Mcp` is the first executable MCP consumer of the reusable Corevia Standard. It is a sibling of the API and invokes Identity Application/MediatR handlers directly.

| Tool | Type | Audience | Risk | Scope |
|---|---|---|---|---|
| `identity_get_self_profile` | Atomic | Self | Read | `identity.mcp.self.read` |
| `identity_list_self_sessions` | Atomic | Self | Read | `identity.mcp.self.read` |
| `identity_get_user_access_summary` | Business | Admin | Read | `identity.mcp.admin.read` |
| `identity_revoke_user_sessions` | Business | Admin | Sensitive + exact approval | `identity.mcp.admin.write` |

Run the development-only stdio profile (HTTP is the default transport):

```sh
cd IdentityService/IdentityService.Mcp
JWT_SECRET='<redacted>' \
COREVIA_MCP_BEARER_TOKEN='<short-lived-token>' \
ASPNETCORE_ENVIRONMENT=Development \
MCP_TRANSPORT=stdio \
dotnet run
```

The process writes logs to stderr and reserves stdout for MCP JSON-RPC frames. HTTP is the default transport (`MCP_TRANSPORT=http`): loopback uses `127.0.0.1`/`::1`, `/mcp` requires JWT authentication and Host/Origin checks, and `/health` is anonymous. Customer-server public access requires HTTPS; private non-loopback HTTP requires explicit `MCP_PRIVATE_NETWORK_TRUSTED=true`. Do not expose localhost through public forwarding or an unauthenticated reverse proxy.

Admin MCP calls require one unambiguous `tenant_id` claim. User access/refresh tokens receive it only when the user has one membership or exactly one default membership; an M2M token receives it only from the explicitly configured client tenant binding. Missing or ambiguous tenant context is denied.

The server, never the Agent prompt, derives Actor/Subject/Tenant. Self rejects identity selectors; Admin is tenant-scoped; Delegated context is signed, short-lived, audience/tool/scope/tenant-bound, anti-replay protected, and requires delegated-subject membership in the tenant. Sensitive session revocation requires exact approval. Raw access/refresh tokens, secrets, OTP, signing material, database credentials, and unredacted sensitive payloads are never returned. Security-management tools are not exposed in the MVP.

The current installation is deployment-neutral and has no central Gateway dependency. A cloud AI can use a local installation only through a trusted local MCP client/Desktop Connector and customer-approved egress. The future enterprise path is AD/LDAP → Keycloak User Federation → OIDC/OAuth2; MCP never binds directly to LDAP.

## Configuration

### appsettings.json (`IdentityService/IdentityService.Api/appsettings.json`)

- `Jwt`
  - `Jwt:Issuer`
  - `Jwt:Audience`
  - `Jwt:AccessTokenMinutes`
  - `Jwt:RefreshTokenDays`
- `Swagger`
  - Title, version, description, and UI options
- `IdentityDb`
  - `IdentityDb:Provider` defaults to `Postgres`; set to `SqlServer` to use the SQL Server adapter
  - `IdentityDb:CommandTimeoutSeconds`

### .env (`IdentityService/IdentityService.Api/.env`)

Local `.env` is bootstrap-only. Centralized values are loaded at runtime through Consul/Vault:

- Shared bootstrap provider keys:
  - `CONSUL_HTTP_ADDR`
  - `CONSUL_HTTP_TOKEN`
  - `CONFIG_CENTER_PREFIX`
  - `VAULT_ADDR`
  - `VAULT_TOKEN`
  - `SECRET_STORE_PATH`
- Local non-sensitive runtime values:
  - `ASPNETCORE_ENVIRONMENT`
  - `IDENTITY_APP_VERSION`
  - `OTP_BASE_URL`
  - `OTP_HTTP_TIMEOUT_SECONDS`

OTP upstream endpoint resolution contract:
- primary source: `ServiceDiscovery:Services:Otp` (`ServiceName`/`ServiceNames`/`Scheme`)
- compatibility fallback: `OTP_BASE_URL` from environment-loaded `IConfiguration` when discovery is unavailable or no healthy endpoint is returned
- the fallback is resolved once during HttpClient registration; `OtpRestClient` must not override `BaseAddress` again (it uses the named Kit REST provider `IdentityOtp`)
- `OTP_HTTP_TIMEOUT_SECONDS` or `IdentityOtp:TimeoutSeconds` controls the Identity -> OTP request timeout; default is 8 seconds and accepted range is 1-30 seconds
- OTP timeout is returned as a controlled upstream timeout instead of allowing frontend/server calls to hang until their own timeout
- A successful send returns `202 Accepted` with `{ "accepted": true, "delivery": "queued" }`; this confirms queue acceptance, not handset delivery

Secrets (DB credentials, `JWT_SECRET`, and `M2M_*_SECRET`) must not be hardcoded in local `.env` and must be resolved from Vault. The selected database provider may also be overridden with `DB_PROVIDER`; when unset, IdentityService keeps the existing Postgres behavior.

### M2M Scope Notes

The `payment-service` M2M client must include `customer.read` in addition to invoice, order, catalog, and notification scopes. PaymentService uses this scope after successful wallet top-up and checkout orchestration to read the customer by phone from CustomerService, refresh the read-only customer balance for notification parameters, and avoid post-payment `403 Forbidden` failures after receipt creation.

## Database Initialization & Seed

Database initialization and seed data run on startup through the infrastructure database adapter. This ensures the schema exists and required roles/scopes/clients are seeded before API requests.

- Postgres is the default path and continues to use EF Core migrations (`Database.Migrate`) instead of `EnsureCreated`, so existing schema changes apply as before.
- If your Postgres database was created by older versions using `EnsureCreated` (no `__EFMigrationsHistory`), `/v1/api/Identity/Verify` may fail with a 500 due to schema drift. To fix this legacy state (DATA LOSS), set `IDENTITY_DB_RESET=true` once and restart to drop/recreate the `public` schema.
- SQL Server is selected only when `IdentityDb:Provider` or `DB_PROVIDER` is `SqlServer`/`Sql`. It uses SQL Server connection secrets (`SQLSERVER_*` or `MSSQL_SA_PASSWORD`) and creates the schema from the EF model without changing identity application logic.

## Running the Service

### Local (dotnet)

```bash
cd IdentityService/IdentityService.Api
dotnet run
```

Service will listen on port `5270` by default (`ASPNETCORE_URLS=http://+:5270`).

### Docker Compose

```bash
docker compose -f IdentityService/docker-compose.yml up --build
```

- Exposes `identityservice` on `localhost:5270`
- Joins the shared `online-shop` docker network
- Uses shared Postgres from `../corevia-legacy/prod/postgres/docker-compose.yml`

## Swagger & Testing

- Swagger UI: `http://localhost:5270/swagger/index.html`
- Root health/info endpoint: `GET /` – prints environment, port, Swagger URL, base API prefix, and UTC time.

For authenticated endpoints (Profile, Users, Roles, Permissions, Scopes, Clients, Sessions, Tenants), obtain an access token via `/v1/api/Identity/Verify` or `/token` (refresh grant) and pass it as:

```http
Authorization: Bearer {accessToken}
```

## Purpose
This document provides operational and integration guidance for `IdentityService`.

## Scope
- Service behavior overview
- Configuration and usage guidance
- Validation and troubleshooting references

## Prerequisites
- Required runtime dependencies are installed
- Required environment variables are configured
- Required service access is available

## Run / Usage
- Use the run commands and endpoint examples already documented in this file

## Validation / Verification
- Validate documented flows against the current implementation and `scripts/docs/audit.sh`

## Troubleshooting
- Check service logs, configuration values, and dependency connectivity first

## Change Log
- 2026-09-22: Removed the `woosync-service` M2M client baseline and WooSync-related scopes (`woosync.read`, `woosync.write`); `WooSyncService` was removed entirely from the monorepo and migrated to `corevia-sync` as a Woo provider. `didarsync-service` and its scopes are unchanged (DidarSyncService is now marked deprecated but not yet removed).
- 2026-06-12: Added database provider adapter support; Postgres remains the default and SQL Server can be selected by configuration. Bumped runtime version to `1.0.28`.
- 2026-05-30: Added `didarsync-service` M2M client baseline and DidarSync scopes (`didarsync.read`, `didarsync.write`).
- 2026-04-14: Added `woosync-service` M2M client baseline and WooSync-related scopes (`woosync.read`, `woosync.write`).
- 2026-03-29: Standard sections added to align the README with repository documentation governance.

## Ownership
- IdentityService team
