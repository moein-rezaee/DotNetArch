[فارسی](./README.fa.md)

# {{Name}}

Metadata:
- Last Updated: {{Date}}
- Owner: Platform Engineering
- Status: Active
- Version: 1.0.0

## Purpose
{{Description}}

## Scope
- `Corevia.Kit.{{Name}}.Abstractions`: ports and data contracts.
- `Corevia.Kit.{{Name}}.Core`: registry, selection and DI registration.
<!-- dotnet-arch:providers -->

Not in scope: anything a service decides itself (business rules, storage).

## Prerequisites
- .NET 9.

## Configuration
Each package documents its own section in `docs/specs/contracts.md`.

## Run / Usage
```csharp
services.AddCorevia{{Name}}(configuration);
```

## Replaces
Copies of this code that services held locally.

## Troubleshooting
- See the error codes in `docs/specs/contracts.md`.

## Validation / Verification
- `dotnet test tests/{{Name}}` (xUnit, no external service).
- `ruby .gitlab/ci/validate.rb documentation`.

## Change Log
- {{Date}}: Initial package area (1.0.0).

## Ownership
- Owner: Platform Engineering
