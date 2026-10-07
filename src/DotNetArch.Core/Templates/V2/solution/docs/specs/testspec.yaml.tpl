testspec: 1
projects:
  - {name: {{App}}.Domain.Tests, covers: Domain, kind: unit}
  - {name: {{App}}.Application.Tests, covers: Application, kind: unit, fakes: FakeUnitOfWork}
  - {name: {{App}}.Infrastructure.Tests, covers: Infrastructure, kind: integration, database: "in-memory SQLite (EF model is provider-agnostic)"}
  - {name: {{App}}.Api.Tests, covers: Api, kind: integration, host: WebApplicationFactory}
categories:
  Configuration: "examples match code; run by scripts/validate-examples.sh"
commands:
  all: "dotnet test"
  configuration: "bash scripts/validate-examples.sh"
rules:
  - "each use case ships with tests"
  - "no claim of live verification against external servers unless it was run"
