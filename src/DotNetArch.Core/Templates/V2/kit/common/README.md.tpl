[فارسی](./README.fa.md)

# {{Prefix}}.Kit.{{Area}}

## Purpose
{{AreaDescription}} Services depend only on `I{{Area}}` (package `{{Prefix}}.Kit.{{Area}}.Abstractions`) and never reference a provider SDK directly.
Which provider a deployment uses is a configuration decision (`{{Area}}:Provider`), not a code decision.

## Packages
- `{{Prefix}}.Kit.{{Area}}.Abstractions` - contracts, records and typed exceptions. No third-party dependencies.
- `{{Prefix}}.Kit.{{Area}}.Core` - options binding and validation, provider selection and the single entry point `Add{{Area}}Kit`.
{{ProviderPackageList}}
Dependency direction: `Providers.* -> Core -> Abstractions`. Core never references a provider package.

## Prerequisites
- Application and Domain layers reference only `{{Prefix}}.Kit.{{Area}}.Abstractions`.
- The composition root references `{{Prefix}}.Kit.{{Area}}.Core` plus every provider the service may be deployed with.

## Usage
```csharp
// composition root
{{RegistrationSnippet}}
services.Add{{Area}}Kit(configuration);   // picks the provider from {{Area}}:Provider

// anywhere else: depend on the abstraction only
public sealed class Consumer(I{{Area}} {{AreaVariable}}) { }
```

## Configuration
Non-sensitive keys (appsettings / configuration center, PascalCase colon keys):

| Key | Meaning |
| --- | --- |
| `{{Area}}:Provider` | Provider name ({{ProviderNames}}). Required when more than one provider is registered. |
{{ConfigTable}}
Secrets (environment / secret store, UPPER_CASE, never in appsettings):

| Key | Used by |
| --- | --- |
{{SecretTable}}
## Failure behavior
- Missing or invalid options throw `InvalidOperationException` naming the key; no silent fallback.
- An unknown `{{Area}}:Provider` throws at first resolve and lists the registered providers.
- Runtime failures surface as `{{Area}}Exception` (stable `ErrorCode`) or a derived type.

## Build, pack, publish
```bash
dotnet build -c Release
scripts/pack.sh        # packs to ./artifacts; pushes when NUGET_SOURCE and NUGET_API_KEY are set
```
The kit is versioned on its own (`Version` in `Directory.Build.props`) and can be built, packed and pushed by CI independently of any service.

## Specs
- [Overview](./docs/specs/overview.md) - [Contracts](./docs/specs/contracts.md) - [Acceptance](./docs/specs/acceptance.md) - [Changelog](./docs/specs/changelog.md)
