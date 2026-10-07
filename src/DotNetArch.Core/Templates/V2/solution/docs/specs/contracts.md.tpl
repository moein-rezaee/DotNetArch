# {{App}} - contracts

## HTTP API
Base path `/api`. Errors use RFC 7807 problem details (400 validation / business rule, 404 not found, 500 unexpected).

| Entity | Route | Operations |
| --- | --- | --- |
<!-- dotnet-arch:entities -->

## Configuration
| Key | Kind | Where |
| --- | --- | --- |
| `Database:Provider` | non-sensitive | appsettings |
| `Database:MigrateOnStartup` | non-sensitive | appsettings |
| `DATABASE_CONNECTION_STRING` | secret | environment / `.env` |
| `Cors:AllowedOrigins` | non-sensitive | appsettings |

Kit keys are added here by `dotnet-arch new kit` / `new service`; see each kit's README for its own keys and secrets.

## Events
Domain events are raised by entities, collected by the unit of work and published after a successful commit as `DomainEventNotification<TEvent>`.
