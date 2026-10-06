# Architecture and final trees

Satisfies R-A1..A8, R-B3..B8, R-E5. Names in `<>` are substituted at generation time.

## 1. DotNetArch tool repository (target)

```text
DotNetArch/
├── AGENTS.md                         rules for agents (tool repo)
├── README.md / README.fa.md
├── DotNetArch.csproj / .sln          single global tool `dotnet-arch` (D-13)
├── global.json
├── scripts/                          run.sh run.ps1 smoke.sh
├── docs/
│   ├── ROADMAP.md / ROADMAP.fa.md    phased plan, ticked; resume point
│   ├── decisions/decisions(.fa).md
│   └── specs/                        requirements overview architecture contracts acceptance changelog
│                                     openspec.yaml testspec.yaml (each with .fa.md pair where prose)
├── samples/                          read-only references: MediaStorage, corevia-identity
└── src/                              (folders inside the single project)
    ├── Program.cs                    entry only: parse -> dispatch
    ├── Commands/                     one class per CLI command (new solution|crud|action|event|enum|constant|service|kit, exec, remove migration, mcp serve)
    ├── Config/                       dotnet-arch.yml model, load/save, validation
    ├── Infrastructure/               ProcessRunner (no shell), FileWriter, GitDetector, Prompts, Logger
    ├── Mcp/                          MCP server (stdio): tools wrapping Commands
    ├── Scaffolding/
    │   ├── Solution/                 layers, DI, config, Program split
    │   ├── Entity/                   crud, action, event, enum, constant
    │   ├── Kits/                     abstractions, core, providers, docs, ci job
    │   ├── Tests/                    per-layer test projects (generated projects only)
    │   ├── Docker/ Git/ Ci/          docker files, git init/host, ci providers + registries
    │   └── McpHost/                  <App>.Mcp host scaffolding
    └── Templates/                    text templates (embedded resources) per area
```

## 2. Generated microservice (layout v2)

```text
<App>/
├── dotnet-arch.yml                   layout: v2, ci, git, registries, kits, entities
├── AGENTS.md  README.md  README.fa.md
├── docs/{specs/{openspec.yaml,testspec.yaml,overview,contracts,acceptance,changelog}, roadmaps/, decisions/}
├── .env.example  .gitignore  .dockerignore  global.json
├── Directory.Build.props  Directory.Packages.props  NuGet.config(optional, registries)
├── docker-compose.yml
├── <ci file for detected provider>   .gitlab-ci.yml | .github/workflows/ci.yml | azure-pipelines.yml | bitbucket-pipelines.yml
├── src/
│   ├── <App>.Domain/                 no project/package references
│   │   ├── Entities/<Entity>.cs      private setters, behaviour methods
│   │   ├── ValueObjects/  Events/  Exceptions/  Enums/  Constants/
│   ├── <App>.Application/            refs: Domain. packages: MediatR, FluentValidation, Logging/Options abstractions
│   │   ├── Abstractions/             ports: IUnitOfWork, IRepository<T>, IClock, external ports (Kit abstractions referenced here)
│   │   ├── Common/                   Behaviors (validation, logging), Pagination, Results
│   │   ├── Features/<Entity>/        vertical slice for one entity
│   │   │   ├── Commands/<Name>/      <Name>Command, <Name>Handler, <Name>Validator
│   │   │   ├── Queries/<Name>/       <Name>Query, <Name>Handler, <Name>Validator
│   │   │   ├── Actions/<Name>/       domain actions (state transitions) command + handler
│   │   │   ├── Events/               domain event handlers
│   │   │   └── Dtos/
│   │   └── DependencyInjection.cs    AddApplication()
│   ├── <App>.Infrastructure/         refs: Application (+Domain). packages: EF Core + provider, Kit Core/Providers
│   │   ├── Persistence/{Context,Configurations,Migrations,Repositories,UnitOfWork}
│   │   ├── Services/                 adapters for ports
│   │   └── DependencyInjection.cs    AddInfrastructure(configuration) (+ Kit Add*())
│   ├── <App>.Api/                    refs: Application, Infrastructure (composition root). packages: AspNetCore, Swagger, Auth
│   │   ├── Controllers/<Entity>/<Entity>Controller.cs   (or Endpoints/<Entity>/ for minimal API)
│   │   ├── Configuration/            AddAppConfiguration(), options binding, ServiceCollectionExtensions per concern
│   │   ├── Program.cs                short: calls extension methods only
│   │   ├── appsettings.json  appsettings.example.json  .env.example  Dockerfile
│   └── <App>.Mcp/ (optional)         refs: Application, Infrastructure. package: ModelContextProtocol.AspNetCore
│       ├── Tools/<Entity>/           one tool class per entity, dispatches same MediatR requests
│       └── Program.cs  appsettings(.example).json  Dockerfile
└── tests/
    ├── <App>.Domain.Tests/  <App>.Application.Tests/  <App>.Infrastructure.Tests/  <App>.Api.Tests/  <App>.Mcp.Tests/
```

Dependency rule: `Domain <- Application <- Infrastructure`; `Api` and `Mcp` reference Application and Infrastructure only
to compose. Application never references Infrastructure or a Kit Core/Provider; it references Kit Abstractions only.

## 3. Kits (independent, D-05)

```text
kits/
├── Directory.Build.props             shared metadata; each kit overrides version
├── <Area>/                           business area: MediaStorage | Cache | MessageBroker | ...
│   ├── <Prefix>.Kit.<Area>.Abstractions/     contracts, records, typed exceptions; no provider/3rd-party deps
│   ├── <Prefix>.Kit.<Area>.Core/             options binding+validation, provider resolver, shared logic, `Add<Prefix><Area>(configuration)`
│   ├── <Prefix>.Kit.<Area>.Providers.<P>/    thin: own config section, own secret names, `Add<P>Provider()`
│   ├── docs/specs/{overview,contracts,acceptance,changelog}(.fa).md
│   ├── README.md README.fa.md AGENTS.md
│   └── (tests/ only with --with-tests)
Examples: Cache -> Providers.Redis, Providers.InMemory; MessageBroker -> Providers.RabbitMq; MediaStorage -> Providers.Minio, Providers.RustFs
```

Dependency rule: `Providers.* -> Core -> Abstractions`. Core never references a provider. Services reference
Abstractions in Application and Core + chosen Providers in the composition root. Configuration: `<Area>:Provider`
selects the provider; secrets are UPPER_CASE env keys; no hard-coded endpoint defaults.

## 4. Generated CI (per detected provider) jobs
`restore -> build -> test -> validate-examples -> docker build/push (if registry) -> kit pack/push (per kit, changes-only)`.
