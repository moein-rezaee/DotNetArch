[فارسی](./identity-service-repo-migration-roadmap.fa.md) | [Parent](../roadmap.md) | [Identity Specs](../../specs/README.md) | [MCP Roadmap](./identity-service-mcp-roadmap.md)

# Corevia Identity Repository Roadmap

Status: Active (pilot of the service-repo migration)
Created: 2026-10-04

This is the own roadmap of the `corevia-identity` repository, split out of the shared [Market Decomposition Master Roadmap](https://gitlab.getcorevia.ir/corevia/market/-/blob/develop/docs/roadmaps/services/market-decomposition-master-roadmap.md) (Phase C). Only items that concern this service are tracked here. The MCP adapter history is in the [MCP roadmap](./identity-service-mcp-roadmap.md).

## Endpoint / Definition of Done

Identity builds, tests, packages, and deploys from this repository alone (Kit consumed as packages), with its own specs, agent context, MCP sibling, CI-only pipeline, a CD values template for `service-artifact.deploy`, and a complete test suite.

## Phase C steps

| Step | Scope | Status |
| --- | --- | --- |
| 2 | Extraction from monorepo (`docs/evidence/monorepo-extraction-file-diff.md`) | Done |
| 3-4 | Kit migration and Kit drift fixed to 0 errors | Done |
| 5a | Specs separation (`repo-specs.create-or-update`) | Done |
| 5b | Agent-context separation (`repo-agent-context.create-or-update`) | Done |
| 7 | MCP sibling: contract and permission-parity validators | Done (contract and parity validators pass; default transport fixed to http) |
| 8 | CI/CD split (CI-only `.gitlab-ci.yml`, CD values template) | Done (docker build not exercised locally: Docker daemon was not running; `dotnet publish` verified) |
| 9 | Test coverage audit and completion | Done (224 tests; `connectcore service-tests validate` passes) |
| 10 | Removal of IdentityService from the monorepo | Human gate, not started here |

## Tracked follow-ups

- Done: `Corevia.Kit.*` is published to Nexus and every `CoreviaKitPackagesRoot` project-reference was replaced with `PackageReference` (see `docs/specs/changelog.md` 0.11.17); `Directory.Build.props` was deleted. Remaining: `.gitlab-ci.yml` (managed by `service-cicd.split`) still checks out `corevia-kit` for CI via the `service_cicd.kit_stopgap` block — harmless now but needs `service_cicd.kit_stopgap.enabled: false` in `corevia-standards/patterns/service-cicd.split/values.corevia-identity.yaml` followed by `corevia-run apply --approve` to drop it.
- Real credential hygiene: no secret values are kept in this repository; Vault/Consul remain the source.
