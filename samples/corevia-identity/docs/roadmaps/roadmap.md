[فارسی](./roadmap.fa.md)

# Identity Roadmap

## Endpoint / Definition of Done

- The repository builds, tests, packages, and is deployed from its own repository: CI-only pipeline, a CD values template for `service-artifact.deploy`, Kit consumed as packages, and a complete test suite.
- OpenSpec and TestSpec stay source-controlled and validate before implementation work.
- Agent rules, specs, and roadmaps are scoped to Identity only and are bilingual with reciprocal links.

## Current Phase

- [x] Fill service-specific OpenSpec and TestSpec (step 5a).
- [x] Separate agent context and roadmaps (step 5b), MCP sibling verified (step 7), CI/CD split (step 8), test suite completed (step 9).
- [x] Publish Corevia.Kit.* to Nexus and switch from the sibling project-reference stopgap to PackageReference.
- [ ] Human gate: removal of IdentityService from the corevia-market monorepo (step 10).

See the [repository migration roadmap](./services/identity-service-repo-migration-roadmap.md) for the per-step status and the [documentation index](../INDEX.md) for every document.

## Validation

- Run the validator declared in `.corevia/validators.yaml`, then `dotnet test IdentityService.sln` (the sibling `corevia-kit` checkout is required).
