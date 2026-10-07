# Decision log

| ID | Decision | Reason |
| --- | --- | --- |
| D-01 | Clean Architecture with ports and adapters, layers as separate projects | testability, replaceable infrastructure |
| D-02 | Vertical slices per entity with CQRS (MediatR 12.x, Apache-2.0) | cohesion, small focused handlers |
| D-03 | Unit of work + repository over EF Core; async, no `IQueryable` in ports | aggregate persistence without leaking the ORM |
| D-04 | appsettings for non-sensitive values, environment/`.env` for secrets, one load step | twelve-factor style, safe defaults |
| D-05 | External capabilities as independent kits (Abstractions / Core / Providers) | swap providers by configuration, version separately |

Add a row (ID, decision, reason) whenever a choice is made; never rewrite history, supersede it with a new row.
