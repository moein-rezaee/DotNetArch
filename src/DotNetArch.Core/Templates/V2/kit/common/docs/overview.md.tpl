# {{Area}} kit - overview

{{AreaDescription}}

## Scope
Package selection, configuration, registration, failure behaviour and compatibility of the {{Area}} capability.

## Packages
{{ProviderPackageList}}
- `{{Prefix}}.Kit.{{Area}}.Abstractions` - contracts. `{{Prefix}}.Kit.{{Area}}.Core` - options, selection, entry point.

## Design rules
- `Providers.* -> Core -> Abstractions`; Core never references a provider.
- The provider is chosen by `{{Area}}:Provider`; adding a provider never changes consumer code.
- Secrets come from UPPER_CASE environment keys only.
