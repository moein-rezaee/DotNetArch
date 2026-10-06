# AGENTS - {{Prefix}}.Kit.{{Area}}

Applies to this kit folder only.

## Spec Rule
- Keep package-area specs in `docs/specs/`. Any behaviour or contract change must update `overview.md`, `contracts.md` and `acceptance.md` (and the `.fa.md` pairs) in the same change.

## Architecture Rule
- Public contracts (`I{{Area}}`, records, exceptions) live in `{{Prefix}}.Kit.{{Area}}.Abstractions`, which has no provider or third-party dependencies.
- Options binding/validation, provider selection and the single `Add{{Area}}Kit` entry point live in `{{Prefix}}.Kit.{{Area}}.Core`. Core references only Abstractions, never a provider package.
- Keep every provider thin: its own configuration section, its own secret names and its own `Add<Provider>{{Area}}Provider()`. Shared logic belongs in Core, never copied into a provider.
- Services must not reference a provider SDK directly.
- Secrets are read only from UPPER_CASE configuration keys (environment / secret store); never add them to appsettings or give endpoints hard-coded defaults.
- Adding or removing a provider is a package addition/removal plus its registration line; update the README provider table and specs in the same change.

## Versioning Rule
- The kit is independent: bump `Version` in `Directory.Build.props` for every released change (SemVer) and record it in `docs/specs/changelog.md`.

## Testing Rule
- Provider tests use stubs/fakes; do not claim live verification against a real server unless it was actually run.

## Documentation Governance Rule
- Keep bilingual pairs (`.md` + `.fa.md`) synchronised.
