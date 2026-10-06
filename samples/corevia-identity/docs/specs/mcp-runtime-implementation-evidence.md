[فارسی](./mcp-runtime-implementation-evidence.fa.md) | [Specs Index](./README.md) | [Repository Index](https://gitlab.getcorevia.ir/corevia/market/-/blob/develop/docs/INDEX.md)

# IdentityService MCP Runtime Implementation Evidence

> Status: Active implementation baseline; controlled pilot pending
> Version: 0.1.0
> Owner: IdentityService team
> Last Updated: 2026-08-29

## Purpose

Record objective evidence that the IdentityService MCP sibling runtime has been implemented against the Corevia Standard, while keeping remote CI, customer pilot, publication, and production deployment as explicit release gates.

## Scope

This evidence covers only `IdentityService/IdentityService.Mcp` in `corevia-market`:

- sibling composition over Identity Application and Domain;
- server-owned authentication, Actor/Subject/Tenant resolution, Self/Admin/Delegated policy, scope intersection, approval, audit, and redaction;
- the complete Identity capability catalog: 41 API capability entries, 36 active exposable MCP Tools (34 Atomic and 2 Business), and 5 explicitly non-exposable protocol-operation bindings;
- local `stdio` runtime and the guarded HTTP transport implementation;
- source, test, and documentation checks performed on the dedicated implementation branch.

It does not claim a customer pilot, package publication, production deployment, central Gateway deployment, or AD/LDAP/Keycloak integration.

## Prerequisites

- `.NET SDK 9` and the repository's private package credentials when restoring through Nexus;
- a valid runtime `JWT_SECRET` supplied through the environment/Vault, never committed to source;
- for Admin calls, a validated access token with one unambiguous `tenant_id` claim; user tokens derive it from one/default membership and M2M tokens require explicit client tenant binding;
- for delegated or sensitive operations, separately provisioned signing keys and approved signed envelopes;
- the Corevia Standard repository available for manifest/Pattern validation.

## Configuration

`IdentityService.Mcp/appsettings.json` contains non-sensitive defaults only. Runtime overrides are supplied through centralized configuration or environment variables:

| Setting | Purpose |
|---|---|
| `MCP_TRANSPORT` | `http` (default) or `stdio` (development-only; rejected unless `ASPNETCORE_ENVIRONMENT=Development`) |
| `COREVIA_MCP_BEARER_TOKEN` | stdio-only short-lived bearer token; never passed through prompt text or command arguments |
| `MCP_HTTP_BIND_ADDRESS` | numeric bind address; loopback is the default |
| `MCP_HTTP_PORT` | HTTP transport port; `5271` default |
| `MCP_HTTP_HTTPS_ENABLED` | enables HTTPS for non-loopback/customer-server exposure |
| `MCP_PRIVATE_NETWORK_TRUSTED` | explicit operator assertion required for private non-loopback HTTP |
| `MCP_ALLOWED_HOSTS` / `MCP_ALLOWED_ORIGINS` | Host/Origin allowlists for `/mcp` |
| `ASPNETCORE_Kestrel__Certificates__Default__Path` | customer-server HTTPS certificate path supplied by the deployment config/secret provider |
| `ASPNETCORE_Kestrel__Certificates__Default__Password` | customer-server HTTPS certificate password; value `<set via vault://>` supplied only by the secret provider |
| `MCP_DELEGATION_SIGNING_KEY` | Vault/environment-only key for signed delegated contexts |
| `MCP_APPROVAL_SIGNING_KEY` | Vault/environment-only key for exact sensitive-operation approvals |

The Identity seed registers all `identity.mcp.*` scopes and grants only `identity.mcp.self.read` to the default public client. Admin scopes are explicit capabilities; assigning one without a tenant binding does not make an Admin call valid. The API capability catalog classifies authorization permissions separately from protocol operations; the latter remain non-exposable when they would return raw tokens or secrets.

The MCP process loads the Corevia ConfigCenter/SecretStore extensions at the composition root. `IdentityService.Mcp` does not read databases, repositories, SQL, or API routes directly.

## Run / Usage

### Local offline installation

Use `stdio` only as a development process transport (requires `ASPNETCORE_ENVIRONMENT=Development`; HTTP is the default). The MCP client starts the process and exchanges JSON-RPC frames over standard input/output:

```sh
cd IdentityService/IdentityService.Mcp
JWT_SECRET='<redacted>' \
COREVIA_MCP_BEARER_TOKEN='<short-lived-token>' \
ASPNETCORE_ENVIRONMENT=Development \
MCP_TRANSPORT=stdio \
dotnet run
```

Application logs are written to stderr; stdout is reserved for MCP protocol frames. A cloud-hosted AI cannot reach this process directly while it is offline. It must use a trusted local MCP client/Desktop Connector and customer-approved egress.

### Loopback or customer-server HTTP

Set `MCP_TRANSPORT=http`. Loopback HTTP remains authenticated and binds only to `127.0.0.1` or `::1`. Public/customer-server access requires HTTPS; private HTTP requires an explicit trusted-network setting and equivalent network controls. The `/health` endpoint is anonymous for health checks; `/mcp` requires authentication and Host/Origin policy.

There is no current Corevia MCP Gateway dependency. A future managed Gateway must remain an optional relay/policy layer and must not change the Application boundary or bypass customer authorization.

## Validation / Verification

The following evidence was obtained on the dedicated Identity MCP implementation worktree/branch:

| Check | Result | Evidence |
|---|---|---|
| Sibling boundary | PASS | `IdentityService.Mcp` references Application/Infrastructure only at composition; no API project reference or controller/API/EF/repository/SQL access in MCP code. |
| Release build | PASS | `dotnet build IdentityService/IdentityService.Mcp/IdentityService.Mcp.csproj` completed with 0 warnings and 0 errors after the Generic Host transport split. |
| Security/runtime tests | PASS | `IdentityService.Mcp.Tests`: complete catalog/security suite passed with 0 failures. |
| Protocol smoke | PASS | stdio `initialize` and `tools/list` completed; exactly 36 active tools were published and stdout contained only MCP JSON-RPC frames. |
| HTTP negative smoke | PASS | Loopback port `5291`: `/health` returned `200`, anonymous `/mcp` returned `401`, invalid `Host` returned `400`, and the process stopped cleanly. |
| Public-bind guard | PASS | A non-loopback bind without `MCP_PRIVATE_NETWORK_TRUSTED=true` was rejected before the listener started. |
| Customer-server HTTPS profile | PASS | An isolated HTTPS run with a temporary certificate returned `200` from `/health` and `401` for anonymous `/mcp`. |
| Customer private-HTTP profile | PASS | An isolated non-loopback HTTP run with explicit `MCP_PRIVATE_NETWORK_TRUSTED=true` returned `200` from `/health` and `401` for anonymous `/mcp`. |
| API/MCP permission parity | PASS | 41 API capability entries are mapped one-to-one; 36 authorization permissions have active usable Tools, while 5 protocol operations are explicitly classified `mcpExposure: prohibited` and remain non-exposable. Restrictions, grants, and sensitive-operation approval are checked by the Standard validator. |
| Tool policy | PASS | 36 active Tools expose versioned audience/risk/scope/approval/restriction metadata: 34 Atomic and 2 Business; no generic API proxy exists. |
| Self/Admin/Delegated policy | PASS | Selector injection, missing scope, tenant boundary, delegated-subject membership, audience/expiry, delegation replay, and server-derived current-session behavior are covered by tests. |
| Identity scope/tenant integration | PASS | MCP scopes and permissions are seeded; public-client self read is explicit; user tokens carry an unambiguous membership/default tenant claim and M2M tenant binding is configuration-driven. |
| Approval/redaction/audit | PASS | Exact approval binding, current-session binding for all-other-session revocation, nonce use, safe result DTOs, and failure auditing are covered by implementation/tests. |
| Deployment neutrality | PASS | stdio uses the Generic Host without a Kestrel listener; HTTP is opt-in and guarded; central Gateway is not required. |
| Remote CI/MR | PENDING | Must pass on the pushed branch/Merge Request before Phase 7 is closed. |
| Customer pilot/release | PENDING | Requires a separate controlled approval and customer evidence; it is not implied by local tests. |

The repo-wide Corevia validator still reports 24 pre-existing baseline findings in unrelated legacy/configuration/documentation files. No changed MCP file was reported; Platform Engineering owns that baseline cleanup, so this remains an external release gate rather than an MCP implementation finding.

The full-repository documentation audit also reports two pre-existing placeholder-link failures in `docs/templates/TECHNICAL_DOCUMENT_TEMPLATE.fa.md` and legacy section warnings. The changed Identity MCP documents introduce no new pairing or link failure; the MR gate uses the changed-only audit.

Representative local verification commands:

```sh
NUGET_PACKAGES='<workspace>/.codex-nuget-cache' \
NUGET_HTTP_CACHE_PATH='<workspace>/.codex-nuget-http-cache' \
dotnet build IdentityService/IdentityService.Mcp/IdentityService.Mcp.csproj --no-restore

NUGET_PACKAGES='<workspace>/.codex-nuget-cache' \
NUGET_HTTP_CACHE_PATH='<workspace>/.codex-nuget-http-cache' \
dotnet test IdentityService/IdentityService.Mcp.Tests/IdentityService.Mcp.Tests.csproj --no-restore
```

The canonical CI job additionally restores through `.ci/create-nuget-config.sh`, builds Release, runs the tests, lists project references, and fails if `IdentityService.Api` or forbidden direct-access symbols enter the MCP source.

## Troubleshooting

- If stdio output is not valid MCP JSON-RPC, inspect stderr logging configuration; application logs must never be written to stdout.
- If an anonymous HTTP request reaches `/mcp`, verify the JWT bearer configuration and do not disable `RequireAuthorization()`.
- If a non-loopback HTTP bind fails, use HTTPS or explicitly provision the private-network trust setting; never expose a public unauthenticated listener.
- If a delegated or approval envelope is rejected, check its audience, tenant, tool/version, scope, expiry, nonce, and exact resource binding. Do not replace a failed envelope with a prompt confirmation or arbitrary `subjectId`.
- In a multi-replica deployment, replace the in-memory nonce store with a distributed replay-protection store before enabling shared delegated/approval traffic.
- If private package restore fails, use the repository's authenticated Nexus flow in CI; do not commit credentials or replace the canonical registry with an obsolete endpoint.

## Change Log

- 2026-08-29: Reconciled runtime evidence with the complete 41-entry API/MCP parity catalog, 36 active Tools, five explicit non-exposable protocol bindings, and the reusable Standard/Bridge gates.
- 2026-08-28: Added the first runtime implementation evidence for the Identity MCP sibling, including build, test, protocol, security, and deployment-boundary results.

## Ownership

IdentityService team; Platform Engineering owns the reusable Corevia MCP Standard and generic validators.

## Post-extraction verification (Phase C step 7, 2026-10-04)

- `corevia-run validate --pattern mcp-contract.validate` against `.corevia/operations/identity-service-mcp-contract.values.yaml`: `contract-valid`.
- `corevia-run validate --pattern mcp-permission-parity.validate` against `.corevia/operations/identity-service-mcp-parity.values.yaml`: `permission-parity-valid` (41 catalog entries, 36 active permissions each with a usable 1:1 Tool, 5 prohibited protocol operations, no orphan Tool).
- Parity gap found and fixed: the code defaulted `MCP_TRANSPORT` to `stdio`. The standard profile requires HTTP by default with stdio development-only; `McpTransportSelector` now defaults to `http` and rejects `stdio` outside `ASPNETCORE_ENVIRONMENT=Development` (`BadRequestException`, `mcp_stdio_dev_only`). Covered by `McpTransportSelectorTests`.
- `dotnet test IdentityService.Mcp.Tests`: 27 passed (16 existing plus 11 transport-selector cases).
