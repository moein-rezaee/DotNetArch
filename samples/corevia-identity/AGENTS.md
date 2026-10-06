# Agent Instructions

This repository follows Corevia shared standards through `.corevia/follows.yaml`.

Before implementation changes:

- Read `docs/specs/openspec.yaml`, `docs/specs/testspec.yaml`, and `docs/roadmaps/roadmap.md`.
- Use the validators declared in `.corevia/validators.yaml`.
- Keep generated bootstrap metadata source-driven through `.corevia/operations/repo-bootstrap.values.yaml`.

<!-- corevia:agent-context:begin sha256=c7bf352629003d2ca74a53ab113cf4cfeabeef9c465b278b37bfcd5a177618d2 -->
<!-- Scoped to corevia-identity; sections copied verbatim from the shared monorepo agent files. Unrelated services' sections are excluded by contract. -->

## Runtime Config/Secret/Discovery Rule (Mandatory)

- Checked-in configuration examples are protected contract files:
  - Every API service with `.env` must keep `.env.example`; every API service with `appsettings.json` must keep `appsettings.example.json`.
  - Deleting either example file is forbidden. Replacing it with documentation, a generated artifact outside the service directory, or a real runtime file is not allowed.
  - Any code or configuration change that adds, removes, renames, changes the shape of, or changes the meaning/default of a configuration or secret key must update the corresponding `.env.example` and/or `appsettings.example.json` in the same merge request.
  - `.env.example` must remain a complete placeholder-only environment/secret key inventory. `appsettings.example.json` must remain a complete sanitized non-secret configuration model.
  - Reviewers and agents must treat example drift as an incomplete implementation, not optional documentation work.
  - `scripts/customer-config/validate-examples.rb` is a mandatory CI gate; do not bypass or weaken it when changing runtime configuration.
- Service runtime configuration must follow this standard:
  - Non-sensitive centralized config: Consul through `ConfigCenterExtension`.
  - Secrets: Vault through `SecretStoreExtension`.
  - Bootstrap merge into runtime: `ConfigLoaderExtension`.
- In every API service startup, `AddConfigLoaderExtension(...)` must run before options binding.
- Consul key naming contract:
  - Stored path format: slash-separated kebab-case.
  - Runtime binding format: colon-separated PascalCase keys in `IConfiguration`.
- Secret key naming contract:
  - Secret keys loaded from Vault/environment must be UPPER_CASE with single underscores.
  - Examples: `JWT_SECRET`, `VAULT_TOKEN`, `VAPID_PUBLIC_KEY`, `VAPID_PRIVATE_KEY`.
- `appsettings.json` keeps only non-sensitive defaults; secrets must not be hardcoded there.
- Runtime provider sync contract:
  - `.env` is the bootstrap contract for provider addresses/tokens and Vault secret selection.
  - `appsettings.json` is the local/default contract for non-sensitive runtime config and must stay aligned with Consul.
  - Consul app-config prefix format: `CONFIG_CENTER_PREFIX=<company>/<project>/<env>/<service>`.
  - Vault bootstrap format: `VAULT_MOUNT=<company>/<project>` and `SECRET_STORE_PATH=<env>/<service>`.
  - `SECRET_STORE_PATH` must be relative to `VAULT_MOUNT` and must not repeat the mount segment.
  - `SecretStore:Vault:Mount`, `SecretStore:MountPath`, and `secret-store/vault/mount` are forbidden in service `appsettings.json` and Consul app-config; Vault mount is an environment/bootstrap value.
  - Every service repo must carry a runtime-provider manifest and must run the Corevia Standards runtime-provider validator per service before build/deploy.
- Service discovery registration must be maintained in Consul for each deployed service instance with a working health endpoint.
- Service discovery registration/check metadata is operational state and must not be stored as service app-config keys under the Consul config prefix.
- Service discovery consumer targets (upstream dependencies that a service calls) must be configured under a dedicated app-config subtree named `ServiceDiscovery:Services`.
  - Structure contract: each consumer key (for example `Identity`, `Sepidar`, or any other dependency) contains discovery target names and scheme.
  - Required pattern per dependency: `ServiceName` and/or `ServiceNames` (comma-separated fallback list), plus optional `Scheme`.
  - Do not keep discovery consumer settings mixed into provider-specific sections (for example avoid placing discovery keys under `Sepidar/*` or `ServiceAuth/*`).
  - When discovery consumer config exists for a dependency, corresponding direct base-url secret keys must be removed from Vault/app-config unless an explicit spec-defined fallback is required.
  - If discovery provider is unavailable or returns no healthy endpoint, consumer resolution must gracefully fallback to the legacy environment URL key (already loaded in `IConfiguration`) when compatibility fallback is defined in service specs.
- `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`, and service `*_APP_VERSION` keys are env/bootstrap concerns and must not be migrated to Consul app-config keys.
- Base URL preservation contract (mandatory):
  - In all service `.env` files, dependency URL keys (for example `*_BASE_URL`) must never be deleted, emptied, or renamed by refactor/migration/merge conflict resolution.
  - During merge/conflict resolution, if both local and incoming changed these keys, keep a valid final `*_BASE_URL` entry for every existing dependency; default priority is incoming value unless a service spec explicitly requires local override.
  - Removal of any `*_BASE_URL` key is allowed only when the corresponding service spec is updated in the same change and explicitly states the replacement/fallback behavior.
- CI/CD contract for centralized runtime providers:
  - use one shared set of CI variables across services: `CONSUL_HTTP_ADDR`, `CONSUL_HTTP_TOKEN`, `CONFIG_CENTER_PREFIX`, `VAULT_ADDR`, `VAULT_TOKEN`, `VAULT_MOUNT`, `SECRET_STORE_PATH`,
  - do not create per-service duplicates for these seven keys,
  - in each deploy job, derive service-specific prefix/path by appending the service suffix (for example `.../otp-service`, `.../notification-service`) when needed.
- Any change in this runtime standard must update:
  - related service docs/specs,
  - shared extension docs/specs,
  - and relevant AGENTS rules in the same change.

## Runtime Migration Flow (Mandatory)

- Any service onboarding or migration to centralized runtime configuration must follow this implementation flow:
  - audit `appsettings.json`, real `.env`, `docker-compose.yml`, `Program.cs`, and service option classes,
  - remove unused, duplicate, and dead config keys before adding new centralized keys,
  - classify every remaining runtime value as one of:
    - non-sensitive config -> Consul,
    - secret -> Vault,
    - service discovery registration/health metadata -> Consul operational registration (not app-config key/value under service prefix),
  - apply the naming contracts:
    - Consul keys: slash-separated kebab-case under the service prefix,
    - Vault secret keys: `UPPER_CASE` with single underscores,
    - Vault mount/path: `VAULT_MOUNT=<company>/<project>`, `SECRET_STORE_PATH=<env>/<service>`,
  - after local cleanup/classification, create or update centralized Consul config, Vault secrets, and Consul discovery registration before any CI/CD pipeline/deploy script change,
  - verify centralized provider readability and discovery health with real runtime credentials before any CI/CD change,
  - CI/CD changes are blocked until centralized provisioning and readability/health validation are complete,
  - verify provider access with the real runtime tokens/credentials, not by existence checks alone,
  - run `AddConfigLoaderExtension(...)` before any options binding or any extension/service registration that consumes configuration,
  - verify the merged values are available through `IConfiguration`,
  - verify service discovery registration and health endpoint in Consul,
  - run at least one real smoke test against a config/secret dependent endpoint,
  - perform a final cleanup/review of local `appsettings.json` and `.env` after migration so they only keep valid bootstrap/local defaults.
  - when server access is available, provisioning and verification must be executed against real Consul/Vault endpoints (commonly via SSH to the runtime host and localhost provider endpoints on that host).
  - migration completion requires objective evidence, not plan-only statements:
    - at least one successful Consul key read under the service prefix,
    - successful Vault read (`200`) for the service secret path,
    - service discovery check status `passing` for the target service.
  - if provisioning cannot be executed (missing access/credentials), the task must be marked as blocked/incomplete and must not be reported as fully migrated.
- Use these project templates/checklists as the default migration baseline:
  - spec templates: `docs/specs/_templates/runtime-migration-*`
  - operational checklist: `docs/RUNTIME_MIGRATION_CHECKLIST.md` and `docs/RUNTIME_MIGRATION_CHECKLIST.fa.md`
- Migration is not complete if centralized keys exist but runtime tokens cannot read them, if values are not injected into `IConfiguration`, or if discovery registration remains stale/misaligned.
- `appsettings.json` and `.env` are transition artifacts during migration, not long-term mirrors of centralized config/secrets.

## Runtime Failure Handling Rule (Mandatory)

- Silent failure is forbidden in `ConfigCenterExtension`, `SecretStoreExtension`, `ConfigLoaderExtension`, and in service startup code that depends on centralized runtime values.
- When centralized config/secret/discovery bootstrap fails, the implementation must:
  - raise an explicit exception for non-recoverable/runtime-critical failures,
  - provide a concise human-readable message that explains the failing provider/path/prefix without leaking secrets,
  - log the failure through the shared `Logging` extension,
  - surface the failure through the shared `ErrorHandling` patterns where applicable.
- Typical failures that must not be swallowed include:
  - missing required secret/config key,
  - provider authorization failures (`401`/`403`),
  - invalid Vault mount/path or Consul prefix/path,
  - bootstrap ordering mistakes that cause configuration consumers to read before injection,
  - service discovery registration/health synchronization failures.
- Fallback to local config is allowed only when the spec for that service/change explicitly defines the fallback and its operational reason. Implicit silent fallback is not acceptable.
- Explicit exception:
  - `ConfigLoaderExtension` may use fallback to local non-sensitive defaults when centralized providers are unavailable, only if the related service/shared spec explicitly defines this behavior.
  - This fallback must be observable (warning/error log), must not leak secrets, and must not hide invalid critical bootstrap contracts (for example malformed prefix/path or missing required secret after merge).

Identity Service - Blueprint and Rules

- Purpose: OTP-based login, token issuance, refresh/blacklist, user storage
- Endpoints (routes under /v1/api/Identity):
  - POST Send: body { PhoneNumber }; calls OTPService /v1/api/Otp/Send
  - POST Verify: body { PhoneNumber, Code }; if valid ? FindOrCreateByPhone, issue { accessToken, refreshToken }
  - POST Refresh: body { RefreshToken }; rotates and returns new tokens
  - POST Logout: body { RefreshToken }; revokes refresh token and optionally blacklists current access token (via cache)
- Database (PostgreSQL):
  - Config keys: Database:Postgres: { Host, Port, Database, Username, Password }
  - Support ${ENV_VAR} placeholders; build connection string in Infra
  - Entities: User (Id, PhoneNumber, CreatedAt, UpdatedAt), RefreshToken (Id, UserId, Token, ExpiresAt, RevokedAt?)
  - EF Core with Generic Repository + UnitOfWork; EnsureCreated on startup (migrations optional)
- JWT:
  - Config: Jwt: { Issuer, Audience, Secret, AccessTokenMinutes, RefreshTokenDays }
  - Symmetric HMAC; do not log secrets; store/validate refresh tokens in DB
- OTP Integration:
  - Config: Otp:BaseUrl; call /v1/api/Otp/Send and /v1/api/Otp/Verify via HttpClient
- Cache (optional):
  - Use CacheExtension for blacklist/rate-limit; prefer Redis provider
- Program.cs wiring:
  - Env.Load(); Controllers/Swagger; AddCacheExtension if needed; register DbContext (Npgsql), repositories/UoW, OTP HTTP client; MediatR/FluentValidation; CORS; Swagger UI
- Seed data and roles:
- Compose:
  - IdentityService/docker-compose.yml declares postgres and (if used) redis; include ../OTPService/docker-compose.yml for OTP dependency
- Acceptance (Identity):
  - [] 4 projects under IdentityService/\* created and referenced
  - [] DbContext + IRepository<T> + IUnitOfWork implemented
  - [] JWT issuing + refresh rotate + logout implemented
  - [] OTP client calls Send/Verify
  - [] Postgres via env; compose up works
  - [] Swagger reachable; routes casing rules respected

## Documentation Governance Rule (Mandatory)

- Global documentation standard is defined in:
  - `docs/standards/DOCUMENTATION_STANDARD.md`
  - `docs/standards/DOCUMENTATION_STANDARD.fa.md`
- All docs changes must follow that standard:
  - bilingual pairing (`.md` + `.fa.md`) for documentation files; `AGENTS.md` files are exempt,
  - reciprocal language links,
  - required sections for technical/operational docs,
  - synchronized updates with code/config changes.
- Validate documentation quality with:
  - `scripts/docs/audit.sh`
- Release/version governance (mandatory with behavior/runtime changes):
  - bump project/service version source,
  - bump main/real environment version key (not example-only files),
  - update EN/FA docs and related specs in the same change.

### Immutable release version rule (mandatory)

- Before committing any release-affecting service or package change, verify the latest published version in Nexus or the GitLab release catalog.
- Never reuse a published version. Nexus artifacts are immutable even if the current branch does not show the prior publishing commit.
- Increment the version according to the service release pattern before committing, and keep the project metadata, real `.env` version key, release registry, and authorized customer profile synchronized.
- On an immutable-artifact collision, do not retry or overwrite the same version; select the next unused version, update all synchronized sources, and publish again.
- Version uniqueness must be verified before the commit, not repaired after a failed publication.

## Persisted Document Date Rule (Mandatory)

- Whenever an agent creates or changes a persisted business document in any service (invoice, receipt, voucher, payment notice, draft, order, or any similar record), every business-date field must be written at calendar-day precision: use the intended business date with time set to `00:00:00` (equivalent to `DateTime.Date`).
- Never write `DateTime.Now`/`DateTime.UtcNow` directly into a business `Date` field. Resolve the intended date first, then normalize it at the provider/repository write boundary. Preserve the customer/business timezone and calendar day; do not convert a date-only value through UTC in a way that shifts its day.
- Date fields on child records that represent the same business event (for example receipt drafts, POS receipt rows, and voucher header dates) must use the normalized parent business date unless their contract explicitly defines a different business date.
- Audit fields such as `CreationDate`, `LastModificationDate`, `CreatedAt`, and `UpdatedAt` are timestamps and must retain their real creation/update time; this rule must not zero their time component.
- The rule applies to every provider and every creation path, including event handlers, direct commands, POS/payment flows, standalone records, quotation flows, and provider adapters. Do not fix only the currently reported scenario.
- Add or update a regression test and the related acceptance/spec documentation for every new document write path. Before reporting completion, search all provider write paths for unnormalized business-date assignments.
<!-- corevia:agent-context:end -->

# Identity Service Agent Guide (repo-local, authoritative)

This section was brought over from the monorepo `IdentityService/AGENTS.md` (Phase C step 5b) and rewritten for this repository's actual layout. It is the source of truth for Identity agents; the "Identity Service - Blueprint and Rules" text inside the managed block above is the historic baseline copied verbatim by the executor and is superseded where it differs (for example it predates OIDC, Tenants, and the Kit packages). Do not edit the managed block by hand; edit this section. Secret values never belong here: the monorepo guide contained a literal database password in its `.env` section, which was deliberately NOT copied.

## 1. Purpose and scope

- OTP-based customer login (`Send`, `Verify`), JWT access token issuance, refresh token storage and rotation, logout/revocation.
- OIDC provider endpoints (`/.well-known/openid-configuration`, `/.well-known/jwks.json`, `/authorize`, `/token`, `/introspect`, `/revoke`, `/userinfo`), `client_credentials` M2M clients with scopes and secrets.
- Administration surface: Users, Roles, Permissions, Scopes, Tenants, Clients, Sessions, Profile, and their relation endpoints under `v1/api`.
- MCP sibling adapter (`IdentityService.Mcp`) over the same Application use cases.
- Default port `5270`, API prefix `v1/api`. Never calls SepidarGateway.

## 2. Layout (repository root)

- `IdentityService.Api`: controllers, `Program.cs`, `appsettings*.json`, Dockerfile.
- `IdentityService.Application`: CQRS features (`Features/<Area>/{Commands,Queries,DTOs}`), MediatR handlers, FluentValidation validators, JWT service and options.
- `IdentityService.Domain`: entities and ports (`Interfaces`), including the own-storage Port `IRepository<T>` / `IUnitOfWork`.
- `IdentityService.Infrastructure`: OTP client (`Corevia.Kit.Http` based), shared DI.
- `IdentityService.Infrastructure.Database`: EF Core `IdentityDbContext`, `EfRepository<T>`, `UnitOfWork`, `DbInitializer`, migrations, provider selection (Postgres default, SqlServer alternate) through `Corevia.Kit.DatabaseConnection`.
- `IdentityService.Mcp` and `IdentityService.Mcp.Tests`: MCP adapter and its tests.
- `.corevia/`: standards wiring, MCP manifest (`mcp/identity-service.yaml`), API permission catalog (`mcp/identity-api-permissions.yaml`), operation values.
- `docs/specs`: bilingual OpenSpec/TestSpec and contracts (source of truth for behavior). `docs/roadmaps`: repo and MCP roadmaps. `docs/decisions`: MCP decision log. `docs/evidence`: extraction and migration evidence.

## 3. Kit-First and dependencies

- Use existing `Corevia.Kit.*` packages (Config, Secrets, ConfigLoader, Logging, Swagger, ErrorHandling, Http, DatabaseConnection, Auth/JWT pieces already referenced). Never hand-roll what a Kit package provides; fix drift via `.corevia/operations/kit-drift.values.yaml`.
- Kit is consumed as published `Corevia.Kit.*` `PackageReference`s restored from the Nexus NuGet feed (see `NuGet.config`). No sibling `corevia-kit` checkout or `Directory.Build.props` stopgap exists in this repo anymore. `.gitlab-ci.yml` (managed by `service-cicd.split`) still runs a redundant Kit-checkout stopgap job pending a standards-side change to disable `service_cicd.kit_stopgap`.
- Error handling: throw the `Corevia.Kit.ErrorHandling` exception types (NotFound, Validation, Unauthorized, etc.); never invent parallel exception hierarchies in handlers.

## 4. Runtime wiring rules

- `AddConfigLoaderExtension(...)` runs before options binding and any configuration-consuming registration. Consul (config) and Vault (secrets) providers are registered before it.
- Config keys: `Jwt` (Issuer, Audience, AccessTokenMinutes, RefreshTokenDays; secret from Vault `JWT_SECRET`), `IdentityDb:Provider` / `DB_PROVIDER` (Postgres default; SqlServer alternate), own-database connection fields from environment/Vault, OTP via `ServiceDiscovery:Services:Otp` with `OTP_BASE_URL` fallback and `OTP_HTTP_TIMEOUT_SECONDS` / `IdentityOtp:TimeoutSeconds`.
- OTP BaseAddress is resolved once in `IdentityService.Infrastructure/DependencyInjection.cs`; the OTP client must not re-read `OTP_BASE_URL` or override `BaseAddress`.
- `.env.example` and `appsettings.example.json` are protected placeholder-only inventories; update them with any key change. Keep `*_BASE_URL` keys.
- Provider-specific EF wiring belongs only in `IdentityService.Infrastructure.Database`; handlers and controllers stay provider-agnostic.

## 5. Behavior invariants

- Customer (OTP) refresh tokens have no expiry timestamp, rotate on refresh (old token revoked with `ReplacedByTokenId`), and stay valid until logout/revocation; legacy tokens with an expiry keep honoring it.
- `Send` returns `202 Accepted` with `{ accepted: true, delivery: "queued" }`. `Verify`/`Refresh` return `{ accessToken, refreshToken }`.
- Seeded M2M clients (`invoice-service`, `pos-service`, `woosync-service`, `didarsync-service` ...) and scopes are contract; see `docs/specs/contracts.md` before changing any seed.
- Removed services' clients (for example `rubikabot-service`) stay removed.

## 6. MCP sibling rules (normative, Identity profile)

- `IdentityService.Mcp` calls Application/MediatR handlers directly; it never calls controllers, the HTTP API, Gateway routes, EF Core, repositories or SQL.
- Scopes `identity.mcp.{self,admin,security}.{read,write}` are separate from API scopes. Actor, Subject, Tenant, Mode, delegation, approval, and correlation are server-resolved; prompt text or caller-supplied `userId`/`tenantId`/`subjectId` never authorizes anything.
- Every API permission has an exact 1:1 MCP mapping (36 active, 5 protocol operations explicitly `prohibited`); no orphan MCP-only tools. CI and `mcp-permission-parity.validate` fail closed.
- Transport defaults to HTTP (authenticated, Host/Origin-guarded, loopback-safe; HTTPS or trusted private network when non-loopback); `stdio` is development-only. Never publicly forward localhost.
- Sensitive writes need exact approval; audit accepted/denied/failed/cancelled calls with redaction. Never return or log raw tokens, client secrets, OTP, signing keys, or database credentials.
- Validate with `corevia-standards/bin/corevia-run validate --pattern mcp-contract.validate --values corevia-identity/.corevia/operations/identity-service-mcp-contract.values.yaml` (and the `mcp-permission-parity.validate` parity values) from the workspace root, then `dotnet test`.

## 7. Build, test, deploy

- `dotnet build IdentityService.sln` and `dotnet test IdentityService.sln` (needs `../corevia-kit` beside this repo).
- Docker image builds from the workspace parent as build context so the sibling `corevia-kit` is included (see `IdentityService.Api/Dockerfile` header and `docs/specs/changelog.md`).
- CI is CI-only (build, test, lint, security-scan, package). Deployment is a separate governed `service-artifact.deploy` run; this repository never deploys from CI and has no old-version cleanup stage.

## 8. Spec-driven workflow and change governance

- Read `docs/specs/openspec.yaml`, `docs/specs/testspec.yaml`, `docs/specs/contracts.md`, `docs/specs/acceptance.md` before editing code. Any behavior/contract/validation/error-handling change updates the specs (EN and FA) in the same change.
- Docs are bilingual (`.md` + `.fa.md`) with reciprocal links; AGENTS.md is exempt.
- Bump versions (project metadata and real env version key) for behavior/runtime changes, and verify the latest published version first; never reuse a published version.
- Keep Clean Architecture boundaries (Domain <- Application <- Infrastructure/Api/Mcp), CQRS with vertical slices, and Clean Code. Do not couple layers.
- Add or update tests with every behavior change (`docs/specs/testspec.yaml`).
