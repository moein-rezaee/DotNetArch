using DotNetArch.Core.Config;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.NetArch;
using DotNetArch.Core.Scaffolding;
using DotNetArch.Core.Scaffolding.Entities;
using DotNetArch.Core.Scaffolding.Kits;
using DotNetArch.Core.Scaffolding.Ops;
using DotNetArch.Core.Scaffolding.Solution;
using DotNetArch.Core.Scaffolding.V2;
using DotNetArch.Core.Validation;

namespace DotNetArch.Core.Operations;

/// <summary>
/// The generators of the tool (<c>new solution</c>, <c>new crud</c>, <c>add kit</c>, ...) as registry operations. They used to be separate CLI commands and
/// hand-written MCP tools; now one definition feeds both, every one plans first and writes only with apply (D-32).
/// </summary>
internal static partial class ScaffoldOperations
{
    private static readonly string[] Methods = { "GET", "POST", "PUT", "DELETE", "PATCH" };

    private static readonly Dictionary<string, string[]> ValidForMethod = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GET"] = new[] { "GETBYID", "GETALL", "GETLIST" },
        ["POST"] = new[] { "CREATE" },
        ["PUT"] = new[] { "UPDATE" },
        ["PATCH"] = new[] { "PATCH", "UPDATE" },
        ["DELETE"] = new[] { "DELETE" },
    };

    private static readonly HashSet<string> CrudNames = new(new[] { "CREATE", "UPDATE", "DELETE", "GETBYID", "GETALL", "GETLIST", "PATCH" }, StringComparer.OrdinalIgnoreCase);

    private static OperationParameter PathParam() => new("path", "Solution folder (the one with dotnet-arch.yml).", Required: true, Alias: "output", Default: "@saved");

    private static OperationParameter NoMigration() => new("no_migration", "Do not create an EF migration (it can be added later).", ParameterType.Flag);

    public static IReadOnlyList<OperationDefinition> All { get; } = new[]
    {
        NewSolution(),
        NewCrud(),
        NewEntity(),
        NewFeature(),
        NewAction(),
        NewEvent(),
        NewEnum(),
        NewConstant(),
        NewService(),
        NewKit(),
        AddKit(),
        AddMcp(),
        ApplyProfileOperation(),
        SwapKit(),
        ApplyBlueprintOperation(),
        AddCli(),
        CiAdd(),
        DockerAdd(),
        GitSetup(),
        RemoveMigration(),
        Exec(),
        ListEntities(),
        DescribeConfig(),
    };

    private static OperationDefinition Mutating(string name, string description, OperationParameter[] parameters, Func<OperationRequest, OperationResult> run) =>
        new(name, description, OperationKind.Mutating, parameters, run);

    /// <summary>Runs <paramref name="action"/> on the solution in <c>path</c> (or on a plan copy of it).</summary>
    private static OperationResult InSolution(OperationRequest request, string operation, Action<SolutionConfig> action, bool normalize = true)
    {
        var path = FullPath(request.Get("path") ?? throw new ArgumentException("Missing required value 'path'."));
        return ScaffoldRunner.Run(request, operation, path, request.Flag("no_migration"), work => action(ScaffoldRunner.LoadConfig(work, path)), normalize);
    }

    private static string FullPath(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        if (!Directory.Exists(full))
            throw new ArgumentException($"Folder not found: {full}");
        return full;
    }

    private static string[] List(string? value) =>
        (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // ------------------------------------------------------------------------------------------------------------------------------

    private static OperationDefinition NewSolution() => Mutating(
        "new_solution",
        "Create a new solution: a Clean Architecture microservice with CQRS, repository/unit of work, tests, optional Docker/CI/MCP host. Layout v3 (default) follows the ABP standard (src/, test/, layer projects only where files belong, typed client); v2 and legacy remain available. Plan first; writes only with apply.",
        new[]
        {
            new OperationParameter("name", "Solution name: letters, digits and underscores, dots allowed between segments.", Required: true, Positional: true),
            new OperationParameter("output", "Folder in which the solution folder is created.", Required: true, Default: "@cwd"),
            new OperationParameter("database", "Database provider.", Choices: new[] { "SQLite", "SqlServer", "Postgres" }, Default: "SQLite"),
            new OperationParameter("style", "API style: controller or fast (minimal API).", Choices: new[] { "controller", "fast" }, Default: "controller"),
            new OperationParameter("layout", "v3 (ABP, default), v2 or legacy.", Choices: new[] { "v3", "v2", "legacy" }, Default: "v3"),
            new OperationParameter("ci", "CI provider.", Choices: new[] { "auto", "none", "github", "gitlab", "azure", "bitbucket", "gitea" }, Default: "none"),
            new OperationParameter("git_remote", "Git remote URL to configure as origin."),
            new OperationParameter("git_provider", "Git provider of a personal server.", Choices: new[] { "github", "gitlab", "gitea", "azure", "bitbucket" }),
            new OperationParameter("git_host", "Base URL of a personal/self-hosted git server."),
            new OperationParameter("docker_registry", "Private Docker registry (host[/path])."),
            new OperationParameter("nuget_source", "Private NuGet feed URL; credentials come from CI secrets, never from files."),
            new OperationParameter("nuget_source_name", "Name of the private NuGet feed."),
            new OperationParameter("no_docker", "Skip Dockerfile and compose.", ParameterType.Flag),
            new OperationParameter("no_git", "Skip git init.", ParameterType.Flag),
            new OperationParameter("no_tests", "Skip the generated test projects.", ParameterType.Flag),
            new OperationParameter("profile", "Shared profile file (path). Its external_kits are wired into the hosts and .net-arch/profile.yml references it."),
            new OperationParameter("in_place", "Create the solution directly in the output folder instead of a folder named after it (for a repository folder that already exists, such as corevia-<slug>). Existing files are never overwritten.", ParameterType.Flag),
            new OperationParameter("cli", "Also generate a CLI host (src/<App>.Cli), a peer of the MCP host: commands that send the same requests as the API.", ParameterType.Flag),
            new OperationParameter("mcp", "Also generate an MCP host (src/<App>.Mcp) whose tools mirror every entity.", ParameterType.Flag),
        },
        request =>
        {
            var name = request.Get("name")!;
            if (!Identifier.IsValidSolutionName(name))
                throw new ArgumentException($"'{name}' is not a valid solution name. Use letters, digits and underscores; dots may separate segments; start with a letter.");
            var output = System.IO.Path.GetFullPath(request.Get("output")!);
            var layout = (request.Get("layout") ?? "v3").ToLowerInvariant();
            if (layout is not ("v3" or "v2" or "legacy"))
                throw new ArgumentException($"Unknown layout '{layout}'. Use v3, v2 or legacy.");
            var ops = new OpsOptions(request.Get("ci") ?? "none", request.Get("git_remote"), request.Get("git_host"), request.Get("git_provider"), request.Get("docker_registry"), request.Get("nuget_source"), request.Get("nuget_source_name"), request.Flag("no_docker"), request.Flag("no_git"), request.Flag("no_tests"), request.Flag("mcp"), request.Flag("cli"));
            var database = request.Get("database") ?? "SQLite";
            var style = request.Get("style") ?? "controller";
            Directory.CreateDirectory(output);

            return ScaffoldRunner.Run(request, "new_solution", output, skipMigrations: true, work =>
            {
                var startup = $"{name}.{(layout == "legacy" ? "API" : "Api")}";
                var provider = layout == "legacy" && database.Equals("None", StringComparison.OrdinalIgnoreCase) ? "None" : database;
                var generation = new SolutionRequest(name, work, startup, style, provider, layout == "legacy" ? SolutionConfig.LegacyLayout : SolutionConfig.V2Layout, ops, request.Flag("in_place"));
                SolutionGenerator.Generate(generation);
                var solutionDirectory = generation.SolutionDirectory;
                if (request.Get("profile") is { } profilePath)
                    ApplyProfile(solutionDirectory, profilePath);
                if (layout == "v3" && ConfigManager.Load(solutionDirectory) is { } config)
                {
                    config.Layout = SolutionConfig.V3Layout;
                    ConfigManager.Save(solutionDirectory, config);
                    var manual = new List<string>();
                    Normalization.Run(solutionDirectory, manual);
                    foreach (var note in manual)
                        ToolHost.Info(note);
                }
            }, normalize: false);
        });

    /// <summary>Wires the external kits of a shared profile into a new solution and records the profile reference in <c>.net-arch/profile.yml</c>.</summary>
    private static void ApplyProfile(string solutionDirectory, string profilePath)
    {
        var full = System.IO.Path.GetFullPath(profilePath);
        var shared = File.Exists(full) ? NetArchStore.LoadProfileFile(full) : null;
        if (shared is null)
            throw new ArgumentException($"Profile file not found or empty: {profilePath}");

        var config = ConfigManager.Load(solutionDirectory) ?? throw new InvalidOperationException("The new solution has no dotnet-arch.yml.");
        config.SolutionPath = solutionDirectory;
        ExternalKitWiring.Apply(config, shared);

        var reference = new ProfileDefinition { Name = shared.Name, Version = shared.Version, Source = System.IO.Path.GetRelativePath(solutionDirectory, full).Replace('\\', '/') };
        var target = System.IO.Path.Combine(solutionDirectory, NetArchFiles.Folder, NetArchFiles.Profile);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
        File.WriteAllText(target, NetArchStore.ToYaml(reference), new System.Text.UTF8Encoding(false));
    }

    private static OperationDefinition NewCrud() => Mutating(
        "new_crud",
        "Generate the CRUD vertical slice (commands, queries, DTOs, validators, EF mapping, controller/endpoints, tests) for an entity. Plan first; writes only with apply.",
        new[] { PathParam(), new OperationParameter("entity", "Entity name, singular PascalCase.", Required: true), NoMigration() },
        request => InSolution(request, "new_crud", config => CrudScaffolder.Generate(config, Identifier.RequireIdentifier(request.Get("entity")!, "entity name"))));

    private static OperationDefinition NewEntity() => Mutating(
        "new_entity",
        "Generate a domain entity with typed properties and its persistence (EF mapping, DTO, mapping, domain and persistence tests) without CRUD use cases; typed use cases (new_action --input --output) give it behaviour. Plan first; writes only with apply.",
        new[]
        {
            PathParam(),
            new OperationParameter("entity", "Entity name, singular PascalCase.", Required: true),
            new OperationParameter("properties", "Properties as Name:Type[(max)][?][!|#], comma separated: string(200), int, bool, decimal, Guid, DateTime, an enum name; ? optional, ! unique index, # index. For example CodeHash:string(128)!,Status:ActivationStatus,ExpiresAtUtc:DateTime?.", Required: true),
            new OperationParameter("description", "One sentence for the entity summary."),
            NoMigration(),
        },
        request => InSolution(request, "new_entity", config => EntityStep(config, request.Get("entity")!, request.Get("properties"), request.Get("description"))));

    internal static void EntityStep(SolutionConfig config, string entity, string? properties, string? description)
    {
        if (!config.IsV2)
            throw new InvalidOperationException("new_entity needs a layout v2 or v3 solution.");
        EntityV2Generator.Generate(config, Identifier.RequireIdentifier(entity, "entity name"), EntityProperty.ParseList(properties), description);
    }

    private static OperationDefinition NewFeature() => Mutating(
        "new_feature",
        "Generate a use-case group without an entity (for example Setting, Secret, Bundle): the controller and the MCP tools class that typed use cases (new_action --entity=<Feature> --input --output) extend. Plan first; writes only with apply.",
        new[]
        {
            PathParam(),
            new OperationParameter("feature", "Feature name, singular PascalCase.", Required: true),
            new OperationParameter("description", "One sentence for the controller summary."),
        },
        request => InSolution(request, "new_feature", config => FeatureStep(config, request.Get("feature")!, request.Get("description"))));

    internal static void FeatureStep(SolutionConfig config, string feature, string? description)
    {
        if (!config.IsV2)
            throw new InvalidOperationException("new_feature needs a layout v2 or v3 solution.");
        FeatureV2Generator.Generate(config, Identifier.RequireIdentifier(feature, "feature name"), description);
    }

    private static OperationDefinition NewAction() => Mutating(
        "new_action",
        "Add a custom use case to an existing entity: domain method stub, command/query, handler, validator and endpoint. Plan first; writes only with apply.",
        new[]
        {
            PathParam(),
            new OperationParameter("entity", "Existing entity.", Required: true),
            new OperationParameter("action", "Action name, for example Archive (empty: inferred from the method)."),
            new OperationParameter("method", "HTTP method.", Required: true, Choices: Methods),
            new OperationParameter("input", "Typed use case: request properties as Name:Type,Name:Type (a trailing ? makes one optional; string(200) sets the validated length, default 512), for example Code:string,DeviceName:string?,Document:string(8000). Without it the action takes the entity id."),
            new OperationParameter("output", "Typed use case: result as Name:Type,... (a generated result record), a type, or none. Empty: the entity DTO."),
            new OperationParameter("anonymous", "The endpoint needs no authentication ([AllowAnonymous]); for public use cases such as activation.", ParameterType.Flag),
            new OperationParameter("description", "One sentence for the endpoint summary and the MCP tool description."),
            NoMigration(),
        },
        request => InSolution(request, "new_action", config => ActionStep(config, request.Get("entity")!, request.Get("action"), request.Get("method")!, request.Get("input"), request.Get("output"), request.Flag("anonymous"), request.Get("description"))));

    internal static void ActionStep(SolutionConfig config, string entityName, string? actionName, string httpMethod, string? input, string? output, bool anonymous, string? description)
    {
        var method = httpMethod.ToUpperInvariant();
        if (!Methods.Contains(method))
            throw new ArgumentException($"Invalid HTTP method. Allowed methods: {string.Join(", ", Methods)}.");
        var auto = string.IsNullOrWhiteSpace(actionName);
        var action = auto
            ? method switch { "GET" => "GetById", "POST" => "Create", "PUT" => "Update", "DELETE" => "Delete", _ => "Patch" }
            : actionName!;
        if (!auto && CrudNames.Contains(action.ToUpperInvariant()) && ValidForMethod.TryGetValue(method, out var allowed) && !allowed.Contains(action.ToUpperInvariant(), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Action name '{action}' conflicts with HTTP method '{method}'. Allowed for {method}: {string.Join(", ", allowed)}.");
        ActionScaffolder.Generate(config, Identifier.Sanitize(entityName), Identifier.Sanitize(action), method, auto, input, output, anonymous, description);
    }

    private static OperationDefinition NewEvent() => Mutating(
        "new_event",
        "Create a domain event for an entity and optionally subscribers (handlers in other entities' feature folders). Plan first; writes only with apply.",
        new[]
        {
            PathParam(),
            new OperationParameter("entity", "Entity that raises the event.", Required: true),
            new OperationParameter("name", "Event name, for example Created.", Required: true),
            new OperationParameter("subscribers", "Comma-separated entities that react to the event."),
            NoMigration(),
        },
        request => InSolution(request, "new_event", config =>
        {
            var entity = Identifier.Sanitize(request.Get("entity")!);
            var name = Identifier.Sanitize(request.Get("name")!);
            if (!EventScaffolder.EntityExists(config, entity))
                throw new ArgumentException($"Entity '{entity}' does not exist.");
            if (!EventScaffolder.GenerateEvent(config, entity, name))
                return;
            foreach (var subscriber in List(request.Get("subscribers")))
                EventScaffolder.AddSubscriber(config, entity, name, Identifier.Sanitize(subscriber));
        }));

    private static OperationDefinition NewEnum() => Mutating(
        "new_enum",
        "Create an enum, scoped to an entity or common to the domain when the entity is omitted. Plan first; writes only with apply.",
        new[] { PathParam(), new OperationParameter("enum", "Enum name.", Required: true), new OperationParameter("entity", "Entity that owns the enum (empty: common)."), new OperationParameter("members", "Comma-separated members after the generated None = 0, for example Active,Revoked."), NoMigration() },
        request => InSolution(request, "new_enum", config => EnumStep(config, request.Get("entity"), request.Get("enum")!, List(request.Get("members")))));

    internal static void EnumStep(SolutionConfig config, string? entityName, string enumName, IReadOnlyList<string> members)
    {
        var entity = entityName is { } e ? Identifier.Sanitize(e) : null;
        if (entity != null && !EnumScaffolder.EntityExists(config, entity))
            throw new ArgumentException($"Entity '{entity}' does not exist.");
        EnumScaffolder.Generate(config, entity, Identifier.Sanitize(enumName), members);
    }

    private static OperationDefinition NewConstant() => Mutating(
        "new_constant",
        "Create a constants class, scoped to an entity or common to the domain when the entity is omitted. Plan first; writes only with apply.",
        new[] { PathParam(), new OperationParameter("constant", "Constants class name.", Required: true), new OperationParameter("entity", "Entity that owns the constants (empty: common)."), NoMigration() },
        request => InSolution(request, "new_constant", config =>
        {
            var entity = request.Get("entity") is { } e ? Identifier.Sanitize(e) : null;
            if (entity != null && !ConstantScaffolder.EntityExists(config, entity))
                throw new ArgumentException($"Entity '{entity}' does not exist.");
            ConstantScaffolder.Generate(config, entity, Identifier.Sanitize(request.Get("constant")!));
        }));

    private static OperationDefinition NewService() => Mutating(
        "new_service",
        "Add a service. With business logic: an Application service registered in AddApplication(). Without (an external capability): a kit with providers, wired into the composition root. Plan first; writes only with apply.",
        new[]
        {
            PathParam(),
            new OperationParameter("logic", "true: internal service with business logic. false: external capability, generates a kit.", Required: true, Choices: new[] { "true", "false" }),
            new OperationParameter("name", "Service name (when logic is true)."),
            new OperationParameter("entity", "Optional entity whose feature folder hosts the service."),
            new OperationParameter("lifetime", "Service lifetime.", Choices: new[] { "Scoped", "Transient", "Singleton" }, Default: "Scoped"),
            new OperationParameter("area", "Kit area (when logic is false): MediaStorage, Cache, MessageBroker or a new capability name."),
            new OperationParameter("providers", "Comma-separated kit providers, for example Redis,InMemory."),
            new OperationParameter("with_tests", "Also generate the kit test project.", ParameterType.Flag),
        },
        request => InSolution(request, "new_service", config =>
        {
            if (request.Get("logic")!.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                ServiceStep(config, request.Get("name") ?? throw new ArgumentException("name is required for a service with business logic."), request.Get("entity"), request.Get("lifetime"));
                return;
            }

            if (!config.IsV2)
                throw new InvalidOperationException("new_service needs a layout v2 or v3 solution.");
            var area = request.Get("area") ?? throw new ArgumentException("area is required for an external service (kit).");
            ServiceV2Generator.GenerateKit(config, area, List(request.Get("providers")), request.Flag("with_tests"));
        }));

    internal static void ServiceStep(SolutionConfig config, string name, string? entity, string? lifetime)
    {
        if (!config.IsV2)
            throw new InvalidOperationException("new_service needs a layout v2 or v3 solution.");
        ServiceV2Generator.GenerateInternal(config, entity, name, lifetime ?? "Scoped");
    }

    private static OperationDefinition NewKit() => Mutating(
        "new_kit",
        "Generate an independent kit (Abstractions + Core + providers) under kits/<Area>, optionally wiring it into the solution. Plan first; writes only with apply.",
        new[]
        {
            PathParam(),
            new OperationParameter("area", "Kit area, for example MediaStorage, Cache, MessageBroker.", Required: true, Positional: true),
            new OperationParameter("providers", "Comma-separated providers."),
            new OperationParameter("kit_prefix", "Package prefix; defaults to the solution name."),
            new OperationParameter("with_tests", "Also generate the kit test project.", ParameterType.Flag),
            new OperationParameter("no_wire", "Do not wire the kit into the solution.", ParameterType.Flag),
        },
        NewKitRun);

    private static OperationResult NewKitRun(OperationRequest request)
    {
        var path = FullPath(request.Get("path")!);
        return ScaffoldRunner.Run(request, "new_kit", path, skipMigrations: true, work =>
        {
            var config = ConfigManager.Load(work);
            if (config != null)
                config.SolutionPath = work;
            var prefix = request.Get("kit_prefix") ?? config?.KitPrefix;
            if (string.IsNullOrWhiteSpace(prefix))
                prefix = config?.SolutionName;
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("kit_prefix is required outside a solution.");
            var major = config is null ? 8 : PackageVersionResolver.ResolveTargetMajor(config.TargetFramework);
            var info = KitGenerator.Generate(new KitRequest(config?.SolutionPath ?? work, request.Get("area")!, List(request.Get("providers")), prefix, major, request.Flag("with_tests")));
            if (config is not null && !request.Flag("no_wire"))
                KitWiring.Wire(config, info);
            else
                ToolHost.Info("Kit generated outside a solution; wire it with add_kit once it is inside one.");
        });
    }

    private static OperationDefinition AddKit() => Mutating(
        "add_kit",
        "Wire an existing kit (kits/<Area>) into the solution. Plan first; writes only with apply.",
        new[] { PathParam(), new OperationParameter("area", "Kit area to wire.", Required: true, Positional: true) },
        request => InSolution(request, "add_kit", config => KitWiring.WireFromDisk(config, KitGenerator.NormalizeArea(request.Get("area")!))));

    private static OperationDefinition AddMcp() => Mutating(
        "add_mcp",
        "Add an MCP host (src/<App>.Mcp) to a layout v2/v3 solution; existing and future entities and actions get tools that run the same use cases as the API. Plan first; writes only with apply.",
        new[] { PathParam() },
        request => InSolution(request, "add_mcp", config => McpV2Generator.Add(config)));

    private static OperationDefinition ApplyProfileOperation() => Mutating(
        "apply_profile",
        "Wire (or upgrade) the external kits of a shared profile in an existing solution: kit package versions, host registration lines, settings, secrets. Safe to repeat. Plan first; writes only with apply.",
        new[] { PathParam(), new OperationParameter("profile", "Shared profile file (path).", Required: true) },
        request => InSolution(request, "apply_profile", config => ApplyProfile(config.SolutionPath, request.Get("profile")!), normalize: false));

    private static OperationDefinition SwapKit() => Mutating(
        "swap_kit",
        "Replace one kit area by the kits of a shared profile in an existing solution (for example JwtSecurity by Authentication and Authorization): the old packages go, the old registration line is replaced in place by the profile's lines, the new kits are wired. Safe to repeat. Plan first; writes only with apply.",
        new[]
        {
            PathParam(),
            new OperationParameter("profile", "Shared profile file (path) that lists the new kits.", Required: true),
            new OperationParameter("from", "The kit area to remove, for example JwtSecurity.", Required: true),
            new OperationParameter("old_call", "Name of the extension method the old kit was registered with, for example AddJwtSecurityExtension."),
            new OperationParameter("old_using", "Namespace of the old kit to remove from the host, for example Corevia.Kit.JwtSecurityExtension."),
        },
        request => InSolution(request, "swap_kit", config =>
        {
            var full = System.IO.Path.GetFullPath(request.Get("profile")!);
            var shared = File.Exists(full) ? NetArchStore.LoadProfileFile(full) : null;
            if (shared is null)
                throw new ArgumentException($"Profile file not found or empty: {request.Get("profile")}");
            ExternalKitWiring.Swap(config, shared, request.Get("from")!, request.Get("old_call"), request.Get("old_using"));
        }, normalize: false));

    private static OperationDefinition AddCli() => Mutating(
        "add_cli",
        "Add a CLI host (src/<App>.Cli) to a layout v2/v3 solution, a peer of the MCP host: every entity, action and typed use case gets a command that sends the same MediatR request as the API and the MCP tool. Plan first; writes only with apply.",
        new[] { PathParam() },
        request => InSolution(request, "add_cli", config => CliV2Generator.Add(config)));

    private static OperationDefinition CiAdd() => Mutating(
        "ci_add",
        "Add a CI pipeline for the git provider (auto-detected from the remote when 'auto'). Plan first; writes only with apply.",
        new[] { PathParam(), new OperationParameter("provider", "CI provider.", Positional: true, Alias: "ci", Choices: new[] { "auto", "github", "gitlab", "azure", "bitbucket", "gitea" }, Default: "auto") },
        request => InSolution(request, "ci_add", config =>
        {
            var provider = OpsGenerator.ResolveCiProvider(config, request.Get("provider"));
            if (provider is null)
                throw new ArgumentException("provider could not be detected from the git remote; name one.");
            OpsGenerator.AddCi(config, provider);
        }, normalize: false));

    private static OperationDefinition DockerAdd() => Mutating(
        "docker_add",
        "Add Dockerfile, compose file and .dockerignore; optionally set a private registry / NuGet feed. In a v3 solution the compose file goes to etc/docker/. Plan first; writes only with apply.",
        new[]
        {
            PathParam(),
            new OperationParameter("docker_registry", "Private Docker registry (host[/path])."),
            new OperationParameter("nuget_source", "Private NuGet feed URL."),
            new OperationParameter("nuget_source_name", "Name of the private NuGet feed."),
        },
        request => InSolution(request, "docker_add", config =>
        {
            if (request.Get("docker_registry") is { } registry)
                config.DockerRegistry = registry;
            if (request.Get("nuget_source") is { } source)
                config.NuGetSource = source;
            if (request.Get("nuget_source_name") is { } sourceName)
                config.NuGetSourceName = sourceName;
            OpsGenerator.AddNuGetConfig(config);
            OpsGenerator.AddDocker(config);
        }));

    private static OperationDefinition GitSetup() => Mutating(
        "git_setup",
        "Configure the git remote and record the git provider/host (personal servers supported). Plan first; writes only with apply.",
        new[]
        {
            PathParam(),
            new OperationParameter("remote", "Git remote URL."),
            new OperationParameter("git_provider", "Git provider.", Choices: new[] { "github", "gitlab", "gitea", "azure", "bitbucket" }),
            new OperationParameter("git_host", "Base URL of a personal/self-hosted git server."),
        },
        request => InSolution(request, "git_setup", config => OpsGenerator.SetupGit(config, request.Get("remote"), request.Get("git_host"), request.Get("git_provider")), normalize: false));

    private static OperationDefinition RemoveMigration() => Mutating(
        "remove_migration",
        "Remove the last EF migration and roll the database back to the previous one. Without apply it only reports what would be removed.",
        new[] { PathParam() },
        request =>
        {
            var path = FullPath(request.Get("path")!);
            if (ConfigManager.Load(path) is null)
                throw new ArgumentException($"Solution configuration (dotnet-arch.yml) not found in '{path}'.");
            if (!request.Apply)
                return new OperationResult(true, "remove_migration: plan (dry run, add --apply to write) - the last EF migration would be removed and the database rolled back one step.", Plan: new[] { new PlannedChange("Migrations", "modify", "last migration removed") });
            var config = ConfigManager.Load(path)!;
            config.SolutionPath = path;
            var ok = MigrationService.RemoveLast(config, path);
            return new OperationResult(ok, ok ? "remove_migration: applied" : "remove_migration: failed", Applied: ok, ExitCode: ok ? 0 : 1, Error: ok ? null : "The migration could not be removed.");
        });

    private static OperationDefinition Exec() => Mutating(
        "exec",
        "Run the service (dotnet run, or Docker with docker/docker_detach/docker_stop). It runs in the foreground, so over MCP use docker_detach or docker_stop. Without apply it only reports what would run.",
        new[]
        {
            PathParam(),
            new OperationParameter("docker", "Run in Docker (build, create, start, follow the logs).", ParameterType.Flag),
            new OperationParameter("docker_detach", "Run in Docker and leave it running.", ParameterType.Flag),
            new OperationParameter("docker_stop", "Stop and remove the Docker container and image.", ParameterType.Flag),
        },
        request =>
        {
            var path = FullPath(request.Get("path")!);
            var detach = request.Flag("docker_detach");
            var stop = request.Flag("docker_stop");
            var docker = request.Flag("docker") || detach || stop;
            if (!request.Apply)
                return new OperationResult(true, $"exec: plan (dry run, add --apply to run) - {(docker ? (stop ? "stop and remove the Docker resources" : detach ? "build and start in Docker, detached" : "build and start in Docker and follow the logs") : "dotnet run the startup project")} in {path}", Plan: Array.Empty<PlannedChange>());
            if (System.Console.IsInputRedirected && !(docker && (detach || stop)))
                throw new ArgumentException("exec runs in the foreground; use docker_detach or docker_stop when no terminal is attached.");
            var config = ConfigManager.Load(path) ?? throw new ArgumentException($"Solution configuration (dotnet-arch.yml) not found in '{path}'.");
            config.SolutionPath = path;
            ExecService.Run(config, new ExecOptions(docker, detach, stop));
            return new OperationResult(true, "exec: done", Applied: true);
        });

    private static OperationDefinition ListEntities() => new(
        "list_entities",
        "List the entities, kits and settings recorded in the solution's dotnet-arch.yml.",
        OperationKind.ReadOnly,
        new[] { PathParam() },
        request => Describe(request, entitiesOnly: true));

    private static OperationDefinition DescribeConfig() => new(
        "describe_config",
        "Describe the solution configuration: layout, framework, database, API style, CI/git/registry settings and wired kits.",
        OperationKind.ReadOnly,
        new[] { PathParam() },
        request => Describe(request, entitiesOnly: false));

    private static OperationResult Describe(OperationRequest request, bool entitiesOnly)
    {
        var path = FullPath(request.Get("path")!);
        var config = ConfigManager.Load(path) ?? throw new ArgumentException($"No dotnet-arch.yml found in '{path}'.");
        var lines = new List<string>();
        foreach (var (name, state) in config.Entities.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            lines.Add($"entity {name}: crud={state.HasCrud} actions={state.HasAction}");
        foreach (var (area, providers) in config.Kits.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            lines.Add($"kit {area}: {providers}");
        if (!entitiesOnly)
        {
            lines.Insert(0, $"solution={config.SolutionName} layout={config.Layout} framework={config.TargetFramework} database={config.DatabaseProvider} style={config.ApiStyle} port={config.ApiPort}");
            lines.Insert(1, $"ci={config.CiProvider} gitProvider={config.GitProvider} gitHost={config.GitHost} dockerRegistry={config.DockerRegistry} nugetSource={config.NuGetSource}");
        }

        return new OperationResult(true, string.Join('\n', lines), new { config.SolutionName, config.Layout, config.TargetFramework, config.DatabaseProvider, config.ApiStyle, entities = config.Entities.Keys, kits = config.Kits });
    }
}
