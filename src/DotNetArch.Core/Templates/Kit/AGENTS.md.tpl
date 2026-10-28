# AGENTS - {{Name}}

Applies to `packages/{{Name}}/`.

## Spec Rule

- Keep package-area specs in `docs/specs/`.
- Any behavior or contract change must update `overview`, `contracts`, `acceptance` and `changelog` (en and fa) in the same change.

## Architecture Rule

- Keep ports and data contracts in `Abstractions/`; keep implementations in `Core/`; each external provider is its own `Providers.<Name>` package behind a port, never inside Core.
- A provider is added with `dotnet-arch scaffold_provider`; the Kit itself was created with `dotnet-arch scaffold_kit`.
- A provider never leaks its own model past its package: the data contracts of `Abstractions/` are the only language the services speak.
