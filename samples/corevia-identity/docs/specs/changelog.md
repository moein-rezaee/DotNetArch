[فارسی](./changelog.fa.md)

# IdentityService Spec Changelog

## 0.11.17 (2026-10-05)

- Removed the Kit sibling-checkout stopgap: `Corevia.Kit.*` is now consumed exclusively as published `PackageReference`s restored from the Nexus NuGet feed (`NuGet.config`). Every `Corevia.Kit.*` `ProjectReference` through `$(CoreviaKitPackagesRoot)` in `IdentityService.Api`, `IdentityService.Application`, `IdentityService.Infrastructure`, `IdentityService.Infrastructure.Database`, and `IdentityService.Mcp` was replaced with the equivalent versioned `PackageReference`: `Corevia.Kit.ErrorHandling.{Core,Abstractions}` 2.0.0, `Corevia.Kit.DatabaseConnection.{Core,Providers.Postgres,Providers.SqlServer}` 2.0.0 and `Corevia.Kit.DatabaseConnection.Abstractions` 1.0.0 (newly published), alongside the already-published `Corevia.Kit.Logging.{Core,Providers.Console}`, `Corevia.Kit.Swagger.Core`, `Corevia.Kit.JwtSecurity.Core`, `Corevia.Kit.Config.Providers.Consul`, `Corevia.Kit.Secrets.Providers.Vault`, `Corevia.Kit.ConfigLoader.Core`, `Corevia.Kit.ServiceDiscovery.Providers.Consul`, and `Corevia.Kit.Http.Core` at their existing 1.0.x versions. `Directory.Build.props` (which only held the `CoreviaKitPackagesRoot` stopgap property) was deleted. `IdentityService.Api/Dockerfile` and `docker-compose.yml` no longer take a `corevia-kit` BuildKit named/additional build context. `.corevia/operations/migration-followups.yaml`'s `kit-not-on-nexus` entry was removed (resolved); `ci-job-token-kit-read` stays (tracks the still-managed `.gitlab-ci.yml` Kit checkout, see below). No application behavior, config key, or public contract changed; `dotnet build`/`dotnet test` are unaffected (236 tests, all passing).
- `.gitlab-ci.yml` is managed by `service-cicd.split` and was intentionally left untouched: it still runs the `service_cicd.kit_stopgap` block (checks out a pinned `corevia-kit` ref and passes it as a BuildKit build context) even though every `.csproj` now restores Kit from Nexus. This is redundant but harmless, since the Dockerfile simply ignores the now-unused named context. Remaining follow-up (not done here, requires a standards-writing change outside this repo): set `service_cicd.kit_stopgap.enabled: false` in `corevia-standards/patterns/service-cicd.split/values.corevia-identity.yaml` and run `corevia-run apply --approve` to regenerate `.gitlab-ci.yml` without the Kit checkout/stopgap.
- Runtime package version `1.0.53`.

## 0.11.16 (2026-10-05)

- Post-migration gate fixes (`service-migration.validate`): removed the hardcoded `POSTGRES_PASSWORD` default (`options.Postgres.PasswordDefault = "postgres"`) from `IdentityService.Infrastructure.Database`; the Kit providers are wrapped so a missing/blank `POSTGRES_PASSWORD` fails with an `InvalidOperationException` naming the key, and `SQLSERVER_PASSWORD` is required only when `SQLSERVER_USER` is set (SQL Server with neither user nor password keeps the original Integrated Security behavior). Every other default and config key is unchanged; `.env.example` now lists both password keys as placeholders.
- Real readiness: `/health` (liveness) keeps its cheap payload; new `/health/ready` runs ASP.NET Core health checks with an EF Core can-connect database check (3 s timeout) and answers `503` when the database is unavailable. `IdentityService.Mcp` (HTTP host) exposes the same pair anonymously. Kit has no health capability (tracked follow-up).
- Example contracts: `appsettings.example.json` of the Api now lists `Swagger:Documents` as an array like `appsettings.json`; added `IdentityService.Mcp/appsettings.example.json`; `.env.example` gained `COREVIA_MCP_BEARER_TOKEN`, `COREVIA_MCP_APPROVAL`, `COREVIA_MCP_DELEGATION` (placeholders).
- Continuous gate: `.corevia/operations/service-migration.values.yaml`, `service-migration` entry in `.corevia/validators.yaml`, `.corevia/operations/migration-followups.yaml` (owners for Kit-on-Nexus, CI job token, DbInitializer integration tests, Kit gaps, source-structure graph, Docker build proof); the extraction report now explains every dropped/renamed/modified file.
- Tests: 236 in the solution (+10 versus 226: missing-password failures (Postgres; SQL Server user without password), SQL Server Integrated Security, readiness 200/503 and liveness). Runtime package version `1.0.52`.

## 0.11.15 (2026-10-05)

- Clean Architecture layering against Kit v2.0.0: `IdentityService.Application` now references only `Corevia.Kit.ErrorHandling.Abstractions` (exceptions moved to namespace `Corevia.Kit.ErrorHandling.Abstractions.Exceptions`; no `ErrorHandling.Core`/ASP.NET dependency, plus an explicit `Microsoft.Extensions.Logging.Abstractions` reference). `Infrastructure` and `Infrastructure.Database` also use `ErrorHandling.Abstractions`; only `Api` and `Mcp` (composition roots) keep `ErrorHandling.Core` for the middleware.
- `IdentityService.Infrastructure.Database` references DatabaseConnection Abstractions, Core and both Providers explicitly and registers them (`AddCoreviaPostgresProvider().AddCoreviaSqlServerProvider()`) before `AddCoreviaDatabase<IdentityDbContext>`; engine choice is still configuration-driven and every config key/default (`IdentityDb:Provider`/`DB_PROVIDER`, `POSTGRES_*`, `SQLSERVER_*`) is unchanged.
- Added 2 tests (missing provider registration is reported per engine); 226 tests in the solution. Runtime package version `1.0.51`.

## 0.11.14 (2026-10-04)

- Step 9: test coverage audit and completion (`connectcore service-tests`): added `IdentityService.Application.Tests` (115), `IdentityService.Infrastructure.Tests` (63) and `IdentityService.Api.Tests` (19) beside `IdentityService.Mcp.Tests` (27) - 224 tests in the solution and CI test stage. Defects found and fixed: `GetUsersPaged` phone filter used a `Contains(string, StringComparison)` overload EF cannot translate (every `phone` filter threw); `OtpRestClient.VerifyCodeAsync` leaked a raw `JsonException` for a non-boolean 2xx body (now `ExternalServiceException` bad gateway); `DbInitializer` now maps provider connection failures to `ExternalServiceException` `database_unavailable` (503). Values: `.corevia/operations/service-tests.values.yaml`.
- Step 8: CI/CD split via `service-cicd.split` (CI-only `.gitlab-ci.yml`: build, test, lint, security-scan, package; no deploy and no cleanup stage; CD template `.corevia/cicd/deploy.values.example.yaml` for `service-artifact.deploy`). The pipeline checks out the pinned corevia-kit ref (`COREVIA_KIT_REF`) and sets `CoreviaKitPackagesRoot`. `IdentityService.Api/Dockerfile` was fixed to build from this repository root and receive Kit as the BuildKit named context `corevia-kit` (`docker buildx build --build-context corevia-kit=../corevia-kit`; `docker-compose.yml` uses `additional_contexts`). Tracked follow-up: publish Corevia.Kit.* to Nexus, switch to `PackageReference`, remove the Kit checkout, the named context, and `CoreviaKitPackagesRoot`. Two whitespace-formatting fixes make the lint job pass.
- Step 7: MCP default transport changed from `stdio` to `http` (stdio is development-only and rejected outside Development) to match the standard profile; `mcp-contract.validate` and `mcp-permission-parity.validate` pass.
- Step 5b: agent context separated (`repo-agent-context.create-or-update`, managed block plus repo-local Identity guide in `AGENTS.md`; MCP roadmap and decision log imported). Removed the hardcoded `POSTGRES_PASSWORD` fallback from the design-time `IdentityDbContextFactory` (it now throws a clear error when the variable is unset); all other defaults are unchanged.
- Phase C step 5a (specs separation). `docs/specs/openspec.yaml` and `testspec.yaml` were regenerated through `repo-specs.create-or-update` (`corevia-run apply`, values `corevia-standards/patterns/repo-specs.create-or-update/values.corevia-identity.yaml`) and extended with the Identity HTTP/MCP/own-storage contract map. The MCP manifest (`.corevia/mcp/identity-service.yaml`) and the MCP contract/parity/source values were brought over from the monorepo and re-pointed to this repo; monorepo-relative links in `docs/specs/*` and `README*.md` were corrected.
- Phase C steps 2-3: extracted from the corevia-market monorepo (snapshot of `develop` 63cb7d92, see `docs/evidence/monorepo-extraction-file-diff.md`) and corrected all Kit drift. Legacy `shared/*` ProjectReferences were replaced by `Corevia.Kit.*` (stopgap `ProjectReference`s to a sibling `corevia-kit` checkout via `Directory.Build.props`; Nexus publish is a tracked follow-up). The unused `shared/CacheService` reference and the unused `DotNetEnv` PackageReferences were removed. `OtpHttpClient` became `OtpRestClient` on `Corevia.Kit.Http`; `IdentityDbContextFactory` uses `Corevia.Kit.DatabaseConnection`; `McpAuthorizationException` derives from the Kit `ServiceException`; OIDC introspection reuses the Kit-registered JwtBearer validation parameters. Config keys, endpoints, and token formats are unchanged.
- Accepted exceptions (kit-drift values `.corevia/operations/kit-drift.values.yaml`): MCP stdio token validation (Kit JwtSecurity has no standalone validator) and `DbInitializer` raw provider connections (database bootstrap is outside Kit DatabaseConnection scope).
- Startup ordering note: the Kit `AddConfigLoaderExtension` copies already-registered services, so Config/Secrets providers are now registered immediately before it; the MCP stdio host uses a documented shim because the Kit has no `IHostApplicationBuilder` overload.

## 0.11.13 (2026-10-04)

- Updated the runtime package version to `1.0.50` for the consolidated database project build.

## 0.11.12 (2026-10-04)

- Collapsed `IdentityService.Infrastructure.Postgres`/`.Infrastructure.SqlServer` into a single `IdentityService.Infrastructure.Database` project, per the `Corevia.Kit.DatabaseConnection` decision recorded in `corevia-kit/docs/specs/market-shared-documentation-migration-audit.md`. Database provider selection, connection-string building, and EF Core `UseNpgsql`/`UseSqlServer` registration are now delegated to `Corevia.Kit.DatabaseConnection`'s single `AddCoreviaDatabase<TContext>` entry point instead of being hand-rolled per engine; `IdentityService.Infrastructure`'s `AddIdentityInfrastructure` no longer branches on the database engine itself. All existing config keys (`IdentityDb:Provider`, `DB_PROVIDER`, `POSTGRES_*`, `SQLSERVER_*`, `Database:Postgres:*`, `Database:SqlServer:*`, `IdentityDb:CommandTimeoutSeconds`) and defaults are preserved exactly. `Corevia.sln` and `IdentityService.Api/Dockerfile` were updated to reference the new project; no public API/contract change.
- Bumped Identity runtime version to `1.0.49`.

## 0.11.11 (2026-09-30)

- Fixed SQL Server identity startup compatibility for existing customer databases: the initializer now applies the additive `RefreshTokens.ReplacedByTokenId` column idempotently after `EnsureCreatedAsync`, preventing Identity introspection and service-event authentication from failing with SQL schema errors.
- Bumped Identity runtime version to `1.0.48`.

## 0.11.10 (2026-09-27)
- Restored the non-sensitive `woosync-service` M2M client baseline after the monorepo WooSyncService removal. The independent WooCommerce provider in `corevia-sync` still requires this exact client id and `M2M_WOOSYNC_SERVICE_SECRET` from Vault.

## 0.11.9 (2026-09-22)
- Removed the `woosync-service` M2M client and its `woosync.read`/`woosync.write` scope seed entries from `appsettings.json`/`appsettings.example.json` and `IdentitySeedService.cs`, and removed `M2M_WOOSYNC_SERVICE_SECRET` from `contracts.md` and the `acceptance.md` M2M resolution checklist, since `WooSyncService` was removed entirely from the monorepo and migrated to the `corevia-sync` repo as a Woo provider. `DidarSyncService` was left untouched but is now explicitly marked deprecated (superseded by the Didar provider in `corevia-sync`); its `didarsync-service` M2M client and scopes remain in place until a later, separate removal.

## 0.11.8 (2026-09-21)
- Removed the `rubikabot-service` M2M client and the `rubikabot.send` scope grant on `catalog-service` from `appsettings.json`/`appsettings.example.json`, removed the `rubikabot.send` scope seed entry from `IdentitySeedService.cs`, removed `M2M_RUBIKABOT_SERVICE_SECRET` from `.env` and `contracts.md`, since `RubikaBotService` was removed entirely from the monorepo (Phase B1 of the [Market Decomposition Master Roadmap](https://gitlab.getcorevia.ir/corevia/market/-/blob/develop/docs/roadmaps/services/market-decomposition-master-roadmap.md)).
- Identity runtime version bumped to `1.0.46`.

## 0.11.7 (2026-09-18)
- Fixed a critical production outage that broke every login: the hand-written `AddRefreshTokenReplacedByTokenId` migration (added in `0.11.4`) was missing the `[DbContext]`/`[Migration]` attributes EF Core's `MigrateAsync()` needs to discover and run a migration. Without them, the migration is silently skipped -- no error, no warning -- and the startup log still prints `"✅ Database migrations applied successfully"` as if nothing was wrong. Result: the `RefreshTokens.ReplacedByTokenId` column was never created in production Postgres, and every login attempt (`POST /v1/api/Identity/Verify`) failed with `500 server_error` because the handler's final `SaveChangesAsync` tried to insert a `RefreshToken` row referencing a column that did not exist.
- Found by reading live production container logs (`docker logs identity-service`) directly on the server, which showed `Npgsql.PostgresException: 42703: column "ReplacedByTokenId" of relation "RefreshTokens" does not exist`.
- Added `[DbContext(typeof(IdentityDbContext))]` and `[Migration("20260918083443_AddRefreshTokenReplacedByTokenId")]` to the migration class so EF Core recognizes and applies it on the next deploy.
- **Same dangerous pattern found but not fixed** (out of scope for this hotfix): two earlier hand-written migrations (`StandardizeOrderWriteScopeFormat`, `MakeCustomerRefreshTokensNonExpiring`) have the identical missing-attribute defect. `MakeCustomerRefreshTokensNonExpiring`'s effect is apparently covered by a separate raw-SQL workaround in `DbInitializer.EnsureRefreshTokenExpiryIsNullableAsync` (which looks like it was added for exactly this reason). This pattern needs a dedicated follow-up to audit and fix all current and future hand-written migrations.
- Bumped Identity runtime version metadata to `1.0.45`.

## 0.11.6 (2026-09-18)
- Fixed the actual root cause of a persistent CI build failure that had been misdiagnosed twice as a Nexus/network routing issue: `IdentityService.Api/Dockerfile`'s early `COPY` layer (used for the cache-friendly `dotnet restore` step) never copied `IdentityService.Infrastructure.SqlServer.csproj`, so that restore never saw `IdentityService.Infrastructure.SqlServer`'s `ProjectReference` and never fetched `Microsoft.EntityFrameworkCore.SqlServer`. The later `dotnet publish` step (run after `COPY . .`, which does bring the full source tree) then had to restore that one package live for the first time -- but it never passed `--configfile`, so it silently fell back to the plain, credential-less `NuGet.config`, producing a reproducible 401 for exactly that one package on every build, regardless of restore URL.
- Added the missing `COPY` line for `IdentityService.Infrastructure.SqlServer.csproj` and added `--mount=type=secret` + `--configfile` to the `dotnet publish` step so any package it still needs to restore live is authenticated the same way the earlier restore step is.
- Bumped Identity runtime version metadata to `1.0.44`.

## 0.11.5 (2026-09-18)
- Fixed an incorrect checked-in default: `appsettings.json`'s `IdentityDb:Provider` was `"SqlServer"`, contradicting both the documented contract (Postgres is the intended default, preserving production behavior) and the C# fallback (`"postgres"`). Introduced accidentally by an unrelated, broad "fix: some buges on package builder" commit in June 2026; unnoticed because Consul was silently overriding it in production. Reverted to `"Postgres"`.
- Added a startup diagnostic log in `AddIdentityInfrastructure` stating which DB provider was actually resolved and from which config source (`IdentityDb:Provider`, `DB_PROVIDER`, or the hardcoded fallback), so this class of mismatch is observable in container logs going forward instead of only visible by reading source.
- Bumped Identity runtime version metadata to `1.0.43`.

## 0.11.4 (2026-09-18)
- Fixed a spurious-logout bug: refresh-token rotation in `RefreshCommandHandler` was single-use with no grace window, so two overlapping refresh calls for the same session (common on page reload — double-fired effects, multiple tabs, or a retry after a slow request) raced each other. The request that lost the race presented an already-revoked token and was hard-rejected with `invalid_refresh_token`, which the frontend read as a full logout even though the session was still valid.
- Added `RefreshToken.ReplacedByTokenId`, set when a token is rotated. If a revoked token is presented again within a 10-second grace window and its replacement is still valid, the handler now replays that replacement (new access token, same still-valid refresh token) instead of failing the request. Reuse presented outside the grace window, or with no live replacement, is still rejected as before.
- Added migration `AddRefreshTokenReplacedByTokenId`.
- Bumped Identity runtime version metadata to `1.0.42`.

## 0.11.3 (2026-09-09)
- Restored the optional M2M client contract fields used by Identity seed validation: `Optional` and `RequiredSecretKey`.
- Bumped Identity runtime version metadata to `1.0.41`.

## 0.11.2 (2026-09-09)
- Removed the legacy Vault mount from appsettings; runtime provider paths remain environment-owned.
- Bumped Identity runtime version metadata to `1.0.40`.

## 0.7.7 (2026-07-06)

## 0.11.1 (2026-09-03)
- Made `POST /v1/api/Identity/Send` return an explicit `202 Accepted` payload for queued OTP delivery.
- Coordinated the client-facing contract with asynchronous OTP notification dispatch and bumped Identity/MCP runtime metadata to `1.0.39`.

## 0.11.0 (2026-08-29)

- Completed the first full Identity API/MCP permission-parity baseline: 41 catalog entries, 36 active authorization Tools (34 Atomic and 2 Business), and five explicit non-exposable protocol-operation bindings.
- Added fail-closed parity validation for new API permissions, missing mappings, orphan Tools, unusable Tools, restriction drift, explicit MCP grant isolation, and raw token/secret safety classification.
- Synchronized bilingual acceptance, overview, roadmap, runtime evidence, and flow-verification documentation with the complete catalog and reusable Corevia Standard/Bridge contracts.
- Made the remaining closure gates explicit: three-repository commit/push/MR/CI, Phase 7 real MCP smoke, Phase 8 customer pilot/release, and the future reusable child executor.
- Bumped the Identity/MCP runtime patch version to `1.0.38`.

## 0.10.0 (2026-08-28)

- Implemented the first executable `IdentityService.Mcp` sibling runtime over Application/MediatR, with no API mirror or direct data access.
- Added the initial Atomic/Business catalog, server-owned Self/Admin/Delegated authorization, tenant boundaries, exact sensitive approval, redacted output, and audit coverage.
- Added Generic Host stdio and guarded HTTP transport support, including stdout protocol isolation, loopback safety, and explicit customer-server network controls.
- Added runtime implementation evidence, updated bilingual contracts/acceptance/overview documentation, and a CI job for Identity MCP build, test, and boundary checks.
- Added delegated-subject tenant-membership validation and synchronized the Identity/MCP release version to `1.0.37`.
- Kept remote CI/MR verification, customer pilot, publication, production deployment, central Gateway, and enterprise federation as explicit pending gates.

## 0.9.4 (2026-08-28)

- Bumped the IdentityService patch version to 1.0.36 so the MCP manifest and governance corrections pass repository change policy.

## 0.9.3 (2026-08-28)

- Recorded the Identity-only MCP facade verification, including the expected approval and read-only apply gates.
- Documented the correction and source verification of the manifest's Application Query/Command entry points.

## 0.9.2 (2026-08-28)

- Corrected the Identity MCP manifest's Application entry points to the actual Query/Command contracts and verified each declared source path.

## 0.9.1 (2026-08-28)

- Added the service-level `mcp-service.create-or-migrate` values contract as the single entry point for Identity MCP work.
- Registered the generated Corevia Skill and routed Identity source discovery to the create or migration child Pattern without a manual Skill or a mandatory Gateway.
- Added deterministic validation evidence for both the new-source and existing-source branches.

## 0.9.0 (2026-08-13)

- Adopted the reusable Corevia MCP Source Standard instead of keeping generic MCP rules only in IdentityService documentation.
- Added the machine-readable Identity MCP source manifest and the read-only `mcp-contract.validate` values contract.
- Mapped Identity-specific acceptance scenarios to the generic Corevia MCP A01–A16 baseline.
- Kept Identity-specific scopes, internal-admin/customer-delegated audiences, Application mappings, MVP allowlist, redaction profile, and deployment profile in the service documentation.

## 0.8.0 (2026-08-13)

- Added the deployment-neutral sibling MCP architecture contract next to `IdentityService.Api`.
- Added explicit MCP self, admin, delegated, security, actor/subject, tenant-boundary, approval, audit, and redaction rules.
- Added the combined atomic/business tool model and prohibited generic API/SQL/repository tools.
- Added local `stdio`/loopback and customer-server HTTPS deployment contracts, including the Cloud AI local-client boundary.
- Recorded `Active Directory (AD/LDAP) -> Keycloak User Federation -> OIDC/OAuth2` and the central Corevia MCP Gateway as optional future enterprise integrations, not current runtime dependencies.

## 0.7.5 (2026-08-05)

- Explicitly bind the EF migrations assembly and repair legacy `RefreshTokens.ExpiresAt` nullability during startup before applying migrations.
- Bumped IdentityService to `1.0.35`.

## 0.7.4 (2026-07-01)

- Declared the Invoice/POS M2M seed secret keys explicitly and made missing financial secrets or scopes fail startup.
- Bumped IdentityService runtime version metadata to `1.0.31`.

## 0.7.6 (2026-07-06)

- Added `pos.payment.start` and `invoice.payment.confirm` scopes.
- Added default `invoice-service` and `pos-service` M2M client metadata for the secured POS flow.
- Added `M2M_POS_SERVICE_SECRET` to the runtime secret contract.
- Bumped IdentityService runtime version metadata to `1.0.30`.

## 0.7.5 (2026-06-12)

- Added database provider adapter support for IdentityService: Postgres remains the default provider and SQL Server can be selected with `IdentityDb:Provider` or `DB_PROVIDER` without changing identity business logic.
- Bumped IdentityService runtime version metadata to `1.0.28`.

## 0.7.4 (2026-06-03)

- Added `order.write` to the default `didarsync-service` M2M client scopes so DidarSyncService can dispatch Didar orders through OrderService's authenticated create-order endpoint.
- Bumped IdentityService runtime version metadata to `1.0.27`.

## 0.7.3 (2026-05-30)

- Added DidarSync authorization seed coverage: `didarsync.read`, `didarsync.write`, related operational permissions, and the default `didarsync-service` M2M client metadata.
- Added `M2M_DIDARSYNC_SERVICE_SECRET` to the Identity runtime secret contract.

## 0.7.2 (2026-04-14)

- Added explicit API project reference to `ServiceDiscoveryExtension`.
- Added and verified centralized `ServiceDiscovery:Services:Otp` consumer keys in Consul.
- Removed unused `Logging` section from `IdentityService.Api/appsettings.json` because it is not consumed by the current shared logging extension.
- Normalized Consul discovery registration to canonical operational identifiers:
  - `ServiceID=nikplus-prd-online-shop-identity-service-5270`
  - `CheckID=service:nikplus-prd-online-shop-identity-service-5270`
- Refreshed runtime migration evidence with real server-side Consul/Vault/readability and Swagger smoke verification.

## 0.7.1 (2026-04-11)

- Standardized Identity scope naming to dot-separated format for M2M scope contracts.
- Updated seed and default Identity client configuration to use `order.write` (replacing `order:write`).
- Enhanced `/introspect` output for access tokens with `scope` and `client_id` and optional client credential verification.

## 0.7.0 (2026-04-09)

- Migrated IdentityService runtime configuration to centralized providers (Consul config + Vault secrets) with bootstrap merge via `ConfigLoaderExtension`.
- Enforced startup ordering contract in `Program.cs` (`AddConfigLoaderExtension(...)` before options binding and config-dependent registrations).
- Replaced local `.env` hardcoded runtime secrets with bootstrap-only provider connectivity keys.
- Updated CI deploy contract to shared six-key provider variables and service suffix derivation (`.../identity-service`).
- Added operational verification requirements for provider readability and discovery health as migration completion gates.
- Migrated full `IdentityClient:M2MClients` metadata (`Name`,`Description`,`Scopes`) to Consul under `identity-client/m2m-clients/*`.
- Kept non-sensitive fallback defaults in `appsettings.json` for resilient startup when providers are temporarily unavailable.

## 0.6.1 (2026-04-05)

- Updated `IdentityService/docker-compose.yml` to consume shared `services` and `data` networks as `external: true`.
- Prevented deployment failures when server networks already exist but are not compose-managed/labeled.

## 0.6.0 (2026-04-05)

## 0.5.0 (2026-04-05)

- Added `rubikabot.send` M2M scope for RubikaBotService integration.
- Extended `catalog-service` M2M client scopes to include `rubikabot.send`.

## 0.4.0 (2026-02-24)

- Updated root SuperAdmin bootstrap phone (`IDENTITY_ROOT_PHONE`) to `09150501658`.
- Added seed permissions for `Catalog.Product.ByCode.Get` and `Catalog.Product.WithoutImageExcel.Get`.
- Mapped `catalog.read` scope to the new catalog product permissions.

## 0.3.0 (2026-02-17)
- Replaced generic baseline with service-logic aware spec content.
- Added explicit responsibility/dependency/negative-scenario contract notes.

## 0.2.0 (2026-02-17)
- Completed service-level baseline specs (`overview`, `contracts`, `acceptance`).

## 0.1.0
- Initial service-level spec baseline.
