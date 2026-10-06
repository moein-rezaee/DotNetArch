# {{Area}} kit - contracts

## Public surface
{{ContractSummary}}

## Registration
`Add<Provider>{{Area}}Provider()` per provider, then `Add{{Area}}Kit(IConfiguration)` once. The last registration of a provider name wins.

## Configuration keys
Non-sensitive:

| Key | Meaning |
| --- | --- |
| `{{Area}}:Provider` | provider name; required when several providers are registered |
{{ConfigTable}}
Secrets (UPPER_CASE environment / secret store keys):

| Key | Used by |
| --- | --- |
{{SecretTable}}
## Errors
`{{Area}}Exception` carries a stable `ErrorCode` (default `{{AreaSnake}}_error`). Option problems throw `InvalidOperationException` naming the key.
