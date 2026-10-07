# معماری و درخت‌های نهایی

این سند R-A1..A8، R-B3..B8 و R-E5 را برآورده می‌کند. نام‌های داخل `<>` هنگام تولید جایگزین می‌شوند.

## ۱. مخزن ابزار DotNetArch (D-17)

```text
DotNetArch/
├── AGENTS.md  README.md  README.fa.md  DotNetArch.sln  global.json  Directory.Build.props  Directory.Packages.props
├── scripts/                          run.sh run.ps1 smoke.sh
├── docs/                             ROADMAP(.fa).md, decisions/, specs/ (requirements overview architecture contracts acceptance changelog openspec testspec)
├── samples/                          read-only references: MediaStorage, corevia-identity (never compiled)
├── src/
│   ├── DotNetArch.Core/              IMPLEMENTATION layer (class library; references nothing of Cli/Mcp)
│   │   ├── Hosting/                  ToolHost, IPrompter, IProcessRunner, IToolOutput (+ default process runner, non-interactive prompter)
│   │   ├── Config/                   dotnet-arch.yml model, load/save, PathState
│   │   ├── Validation/               identifier/path validation, Naming
│   │   ├── Scaffolding/              Solution/ Entity/ (crud action event enum constant) Services/ Kits/ Docker/ Git/ Ci/ Tests/ McpHost/
│   │   └── Templates/                text templates (embedded resources)
│   ├── DotNetArch.Mcp/               MCP LAYER (class library): server bootstrap + tools; non-interactive host
│   └── DotNetArch.Cli/               COMMAND LAYER (exe, PackAsTool, command `dotnet-arch`, package id `DotNetArch`)
│       ├── Program.cs                entry only
│       ├── Commands/                 one class per command; arg parsing -> Core requests
│       └── Console/                  ConsolePrompter, ConsoleOutput, spinner (TTY only)
└── tests/                            per layer where needed (D-02/D-17)
    ├── DotNetArch.Core.Tests/        validation, config round-trip, golden generated-tree tests (fake host)
    ├── DotNetArch.Cli.Tests/         argument parsing
    └── DotNetArch.Mcp.Tests/         tool catalogue and non-interactive behaviour
```

قاعده: `Cli -> Core`، `Mcp -> Core`، `Cli -> Mcp`؛ Core به هیچ‌کدام ارجاع نمی‌دهد و مستقیم از `Console` یا shell استفاده نمی‌کند (D-18).

## ۲. میکروسرویس تولیدشده (layout v2)

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
│   │   ├── Features/<Plural>/        vertical slice for one entity
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
│   │   ├── Controllers/<Plural>/<Entity>Controller.cs   (or Endpoints/<Entity>/ for minimal API)
│   │   ├── Configuration/            AddAppConfiguration(), options binding, ServiceCollectionExtensions per concern
│   │   ├── Program.cs                short: calls extension methods only
│   │   ├── appsettings.json  appsettings.example.json  .env.example  Dockerfile
│   └── <App>.Mcp/ (optional)         refs: Application, Infrastructure. package: ModelContextProtocol.AspNetCore
│       ├── Tools/<Entity>/           one tool class per entity, dispatches same MediatR requests
│       └── Program.cs  appsettings(.example).json  Dockerfile
└── tests/
    ├── <App>.Domain.Tests/  <App>.Application.Tests/  <App>.Infrastructure.Tests/  <App>.Api.Tests/  <App>.Mcp.Tests/
```

قاعدهٔ وابستگی: `Domain <- Application <- Infrastructure`؛ `Api` و `Mcp` فقط برای composition به Application و Infrastructure ارجاع می‌دهند. Application هرگز به Infrastructure یا Core/Provider یک kit ارجاع نمی‌دهد.

## ۳. Kitها (مستقل، D-05)

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

قاعده: `Providers.* -> Core -> Abstractions`. سرویس‌ها Abstractions را در Application و Core و Providerهای انتخابی را فقط در composition root می‌بینند.

## ۴. jobهای CI تولیدشده
`restore -> build -> test -> validate-examples -> ساخت/push ایمیج (با registry) -> pack/push هر kit (با NuGet feed)`.
