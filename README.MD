<img src="assets/icon.png" width="128" height="128" style="vertical-align: middle;"/>

# **DotNetArch: Simplify Clean Architecture Setup**  

#### **Introduction**  

DotNetArch is a .NET global tool designed to help developers quickly set up solutions following Clean Architecture principles and Domain-Driven Design (DDD). This tool streamlines the creation of well-structured .NET projects, saving time and effort while adhering to industry standards.  

---

#### **Requirements**  

Before using DotNetArch, ensure your system meets the following requirements:  

1. **.NET SDK**: Version 6.0 or higher (recommended: .NET 9.0).  
   - Install it from the official [.NET Downloads page](https://dotnet.microsoft.com/download).  
2. **Operating System**:  
   - **Windows**: Windows 10 or higher.  
   - **macOS**: macOS Catalina or higher.  
   - **Linux**: Any distribution supporting .NET Core.  
3. **Git**: Ensure Git is installed if you plan to clone or contribute.  

To check your .NET version, run:  

```bash
dotnet --version
```  

---

#### **How to Install and Use the NuGet Package**  

**Step 1: Install the Tool**  
You can install DotNetArch globally via NuGet with the following command:  

```bash
dotnet tool install --global DotNetArch
```  

**Step 2: Update the Tool**  
To ensure you have the latest version of the tool:  

```bash
dotnet tool update --global DotNetArch
```  

**Step 3: Generate a Clean Architecture Solution**  
Once installed, you can use the tool to create a solution structure:

```bash
dotnet-arch new solution <SolutionName> [--output=Path] [--startup=ProjectName] [--style=controller|fast]
```

`--output` is optional and lets you specify where the solution is created (useful for tests). If omitted, the current directory is used. `--startup` lets you choose which project should act as the startup project (default is `<SolutionName>.API`). `--style` selects between classic **Controller** APIs or lightweight **Fast** minimal APIs (default `controller`). Missing parameters are prompted interactively with defaults shown in brackets; when selecting API style, simply enter **1** for Controller or **2** for Fast. During creation you'll be prompted for a database provider (SQL Server or SQLite; hitting enter selects SQLite). After scaffolding, a `dotnet-arch.yml` file records the solution name, path, startup project, API style, chosen provider, and the scaffolding state of each entity so later commands can skip work already done. Swagger and service registrations are configured at this stage so the API runs immediately.

This command generates the following layers:

- **Core**
- **Application**
- **Infrastructure**
- **API**

#### Generate CRUD for an Entity

After creating a solution you can scaffold a full CRUD vertical slice for an entity:

```bash
dotnet-arch new crud --entity=EntityName [--output=Path]
```

`--output` points to the solution directory; if omitted, the last path saved by the tool is used (falling back to the current directory). If required arguments are missing the CLI prompts for them. The command reads `dotnet-arch.yml` to locate the solution name and scaffolds the entity slice. Depending on the selected style it creates either API controllers or minimal FastAPI endpoints along with domain models, repositories, unit of work, MediatR handlers and validators. The CLI installs `dotnet-ef` if needed and runs migrations for relational providers (SQL Server, SQLite, PostgreSQL) so schema changes are applied automatically. Each entity is placed under a `Features/<Entity>` folder in every layer and follows a CQRS-aware vertical slice:

```
Application/<Entity>/
├── Commands/
│   ├── Create/
│   ├── Update/
│   └── Delete/
└── Queries/
    ├── GetById/
    ├── GetAll/
    └── GetList/
```

Every operation folder under Commands or Queries contains the request, its handler, and a corresponding validator. The first time you scaffold an entity you choose either SQL Server or SQLite and the choice is saved to `dotnet-arch.yml` so later `new crud` runs reuse the same provider. Generated code supports pagination via `PagedResult<T>` and re-running the command updates existing files instead of duplicating them.

All controller routes and endpoint paths are emitted in **PascalCase** (for example, `/Api/Product/All`).

#### Add a Custom Action

Extend an existing slice or create a minimal slice with a single action:

```
dotnet-arch new action --entity=EntityName --action=download --output=../MyApp
```

When running outside the solution directory the command reuses the last saved solution path. Provide `--output` to override it and point to a different `dotnet-arch.yml`. By default the action is treated as a command. To scaffold a query instead, append `--is-command=false`:

```
dotnet-arch new action --entity=EntityName --action=getfile --is-command=false --output=../MyApp
```

The tool generates the command or query, handler, validator, repository method, and controller endpoint for the specified action. If the entity slice is missing, a minimal repository and controller are also created with only this action stub. Migrations are applied automatically after scaffolding so the database stays in sync.

#### Add a Service

Scaffold application or infrastructure services through an interactive menu:

```
dotnet-arch new service [--output=Path]
```

The CLI lets you choose between **Custom**, **Cache**, and **Message Broker** services. During generation you select the desired dependency injection lifetime (scoped, transient, or singleton).

Custom services first prompt for an entity name. When provided, the service is placed under `Application/Features/<Entity>/Services/<EntityService>` with the service name defaulting to `<Entity>Service`. If no entity is supplied, the service name becomes mandatory; internal services land in `Application/Common/Services` while external services are emitted under `Infrastructure/Services/<ServiceName>` with their interfaces in `Application/Common/Interfaces`. Service names are normalized to **PascalCase** and suffixed with `Service` if missing. Registrations are appended to per‑layer `DependencyInjection` extension classes and invoked from `Program.cs`.

Selecting **Cache** or **Message Broker** scaffolds ready‑to‑use Redis or RabbitMQ wrappers. Their NuGet packages are added automatically, interfaces live in `Application/Common/Interfaces` and implementations in `Infrastructure/Services/<Provider>`. These services are registered as singletons, `.env.development`, `.env.test`, and `.env.production` files are created with placeholder credentials, and the startup project loads them via **DotNetEnv**.

#### Run the Startup Project

After scaffolding, launch the API and optionally apply EF Core migrations:

```
dotnet-arch exec [--output=Path]
```

When run, the command asks whether to check for model changes. Choosing **y** creates a migration if needed, applies pending migrations, and then runs the configured startup project. The optional `--output` overrides the last solution path stored by the tool.

#### Manage Migrations

Work with EF Core migrations without starting the application:

```bash
dotnet-arch remove migration [--output=Path]
```

`remove migration` rolls the database back to the previous state and deletes the most recent migration. The command accepts an optional `--output` to override the stored solution path and automatically supplies `--project`, `--startup-project`, and migration folder arguments to `dotnet ef` so you don't have to.

---

#### **How to Clone and Run the Project**

**Step 1: Clone the Repository**  

To get the source code, clone the repository from GitHub:  

```bash
git clone https://github.com/moein-rezaee/DotNetArch.git
cd DotNetArch
```  

**Step 2: Build the Project**  

Ensure you have the .NET SDK installed. Then, build the project:  

```bash
dotnet build
```  

**Step 3: Run the Project**  

To test the functionality, you can execute the project locally:  

```bash
dotnet run
```  

---

#### **How to Contribute**  

We welcome contributions to DotNetArch! Here’s how you can contribute:  

1. **Fork the Repository**:  
   Create a fork of the main repository to your GitHub account.  

2. **Create a New Branch**:  
   Use a meaningful branch name, such as `feature/add-new-command`.  

   ```bash
   git checkout -b feature/add-new-command
   ```  

3. **Make Your Changes**:  
   Ensure your code adheres to .NET coding guidelines.  

4. **Push and Submit a Pull Request**:  
   Push your changes and open a pull request to the main repository.  

   ```bash
   git push origin feature/add-new-command
   ```  

---

#### **Features**

- **Automated Project Setup**: Generate Clean Architecture solutions with a single command.
- **Cross-Platform**: Fully functional on Windows, macOS, and Linux.
- **Domain-Driven Design Support**: Includes pre-configured layers for DDD.
- **Ease of Use**: Intuitive and straightforward CLI commands.
- **`new crud` scaffolding**: Generates vertical-slice CQRS CRUD with MediatR, FluentValidation, EF Core repositories, and paginated results.
- **Database Provider Selection**: Choose between SQL Server, SQLite, PostgreSQL or MongoDB (default SQLite); the selected provider is stored so future runs don't prompt again.
  - *Other providers are experimental and require more testing and development before a NuGet release.*
- **Idempotent Generation**: Re-running commands updates existing files without creating duplicates.
- **Repository & Unit of Work patterns**: Each entity gets its own repository and UoW registration for clean data access.
- **Automatic package & service registration**: Required NuGet packages are added to project files and services like `DbContext`, `IMediator`, `IUnitOfWork`, and validators are registered in `Program.cs`.
- **`new action` command**: Add custom commands or queries with handlers, validators, repository methods, and controller endpoints.
- **Composable actions and slices**: Actions scaffolded before a full `new crud` run are preserved and the later CRUD generation augments the existing files instead of overwriting them.
- **`exec` command**: Run the startup project with an optional migration check that can generate and apply pending EF Core migrations.
- **`remove migration` command**: Roll the database back and delete the most recent EF Core migration.
- **Correct namespace wiring**: `Program.cs` updates include the necessary `using` directives and service registrations for each entity's repository without malformed entries.
- **Configurable startup project**: `dotnet-arch.yml` tracks which project is the startup target so controllers are added to the correct application entry point.
- **Custom output path**: Commands remember the last solution path but accept an `--output` option so code can be generated in test directories or alternative locations.
- **Development Swagger setup**: API projects automatically reference `Microsoft.AspNetCore.OpenApi` and `Swashbuckle.AspNetCore`, register Swagger services, and enable Swagger UI in development while removing default template files and sample endpoints.
- **Feature-based folder structure**: Each layer organizes code under `Features/<Entity>` so all slices stay grouped.
  - **Automatic migrations**: `new crud`, `new action`, and `exec` verify `dotnet-ef` and attempt installation if needed. For relational providers (SQL Server, SQLite, PostgreSQL) migrations and database updates run automatically; for MongoDB the process skips migrations. Generated `Program.cs` calls `Database.Migrate()` on startup to apply any pending migrations so new tables are created automatically.

---

#### **Future Goals**  

- Add support for other architectural patterns such as **Microservices** and **Event-Driven Design**.  
- Improve customization options for solution templates.  
- Add integration with popular libraries and frameworks like **Fluent Validation**, **MediatR**, and **MassTransit**.  
- Provide GUI support for non-CLI users.  

---

#### **Contact Me**  

If you have any questions, suggestions, or issues, feel free to reach out:  

- **GitHub Issues**: [Report Here](https://github.com/moein-rezaee/DotNetArch/issues)  
- **Email**: me.rezaei1996@gmail.com  
- **LinkedIn**: [Moein Rezaee](https://linkedin.com/in/moein-rezaee-26331a125)  

---

Start simplifying your .NET project setup today with **DotNetArch**! 🚀