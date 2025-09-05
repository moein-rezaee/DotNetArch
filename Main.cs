using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DotNetArch.Scaffolding;
using DotNetArch.Scaffolding.Steps;

class Program
{
    static Process? _currentProcess;
    static bool _cancelRequested;

    static void Main(string[] args)
    {
        if (!EnsureDotnetSdk())
            return;
        if (args.Length >= 2 && args[0].ToLower() == "new" && args[1].ToLower() == "crud")
        {
            string? entity = null;
            string? outputPath = null;

            for (int i = 2; i < args.Length; i++)
            {
                if (args[i].StartsWith("--entity="))
                    entity = args[i].Substring("--entity=".Length);
                else if (args[i].StartsWith("--output="))
                    outputPath = args[i].Substring("--output=".Length);
            }

            if (string.IsNullOrWhiteSpace(entity))
                entity = Ask("Enter entity name");
            if (string.IsNullOrWhiteSpace(entity))
            {
                Error("Entity name is required.");
                return;
            }

            entity = SanitizeIdentifier(entity);

            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = PathState.Load() ?? Directory.GetCurrentDirectory();

            var basePath = outputPath!;
            var config = ConfigManager.Load(basePath);
            if (config == null)
            {
                Error("Solution configuration not found. Run 'new solution' first.");
                return;
            }

            CrudScaffolder.Generate(config, entity);
            return;
        }

        if (args.Length >= 2 && args[0].ToLower() == "new" && args[1].ToLower() == "event")
        {
            string? entity = null;
            string? eventName = null;
            string? outputPath = null;
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i].StartsWith("--entity="))
                    entity = args[i].Substring("--entity=".Length);
                else if (args[i].StartsWith("--name="))
                    eventName = args[i].Substring("--name=".Length);
                else if (args[i].StartsWith("--output="))
                    outputPath = args[i].Substring("--output=".Length);
            }

            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = PathState.Load() ?? Directory.GetCurrentDirectory();
            var basePath = outputPath!;
            var config = ConfigManager.Load(basePath);
            if (config == null)
            {
                Error("Solution configuration not found. Run 'new solution' first.");
                return;
            }

            while (true)
            {
                if (string.IsNullOrWhiteSpace(entity))
                    entity = Ask("Enter entity name");
                if (string.IsNullOrWhiteSpace(entity))
                {
                    Error("Entity name is required.");
                    return;
                }
                entity = SanitizeIdentifier(entity);

                if (!EventScaffolder.EntityExists(config, entity))
                {
                    Error($"Entity '{entity}' does not exist.");
                    entity = null;
                    eventName = null;
                    continue;
                }

                var events = EventScaffolder.ListEvents(config, entity);
                if (events.Length == 0 && string.IsNullOrWhiteSpace(eventName))
                    eventName = Ask("Enter event name");

                if (string.IsNullOrWhiteSpace(eventName))
                {
                    var actionOptions = new[] { "Create new event", "Add subscriber to existing events", "Cancel" };
                    var action = AskOption($"Entity '{entity}' has existing events. Select action", actionOptions);
                    if (action == "Cancel")
                        return;

                    if (action == "Create new event")
                    {
                        eventName = Ask("Enter event name");
                        if (string.IsNullOrWhiteSpace(eventName))
                        {
                            Error("Event name is required.");
                            return;
                        }
                        eventName = SanitizeIdentifier(eventName);
                        if (!EventScaffolder.GenerateEvent(config, entity, eventName))
                        {
                            eventName = null;
                            continue;
                        }
                        Success($"Event {eventName} for {entity} generated.");
                        events = events.Append(eventName).ToArray();
                    }
                    else
                    {
                        var eventOptions = events.Concat(new[] { "Back", "Cancel" }).ToArray();
                        var selected = AskOption("Select event", eventOptions);
                        if (selected == "Back")
                        {
                            eventName = null;
                            continue;
                        }
                        if (selected == "Cancel")
                            return;
                        eventName = selected;
                    }
                }
                else
                {
                    eventName = SanitizeIdentifier(eventName);
                    if (!EventScaffolder.GenerateEvent(config, entity, eventName))
                    {
                        eventName = null;
                        continue;
                    }
                    Success($"Event {eventName} for {entity} generated.");
                    events = events.Append(eventName).ToArray();
                }

                var currentEvent = eventName;
                while (true)
                {
                    var options = events.Length > 1
                        ? new[] { "Add subscriber", "Add subscriber for other events", "Finish" }
                        : new[] { "Add subscriber", "Finish" };
                    var choice = AskOption("Select action", options);
                    if (choice == "Add subscriber")
                    {
                        var sub = Ask("Enter subscriber entity");
                        sub = SanitizeIdentifier(sub);
                        if (!EventScaffolder.AddSubscriber(config, entity, currentEvent!, sub))
                            continue;
        
                        Success($"Subscriber {sub} added.");
                    }
                    else if (choice == "Add subscriber for other events")
                    {
                        var eventOptions = events.Concat(new[] { "Back", "Cancel" }).ToArray();
                        var selected = AskOption("Select event", eventOptions);
                        if (selected == "Back")
                            continue;
                        if (selected == "Cancel")
                            return;
                        currentEvent = selected;
                    }
                    else
                    {
                        break;
                    }
                }
                break;
            }
            return;
        }

        if (args.Length >= 2 && args[0].ToLower() == "new" && args[1].ToLower() == "enum")
        {
            string? entity = null;
            string? enumName = null;
            string? outputPath = null;
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i].StartsWith("--entity="))
                    entity = args[i].Substring("--entity=".Length);
                else if (args[i].StartsWith("--enum="))
                    enumName = args[i].Substring("--enum=".Length);
                else if (args[i].StartsWith("--output="))
                    outputPath = args[i].Substring("--output=".Length);
            }
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = PathState.Load() ?? Directory.GetCurrentDirectory();
            var basePath = outputPath!;
            var config = ConfigManager.Load(basePath);
            if (config == null)
            {
                Error("Solution configuration not found. Run 'new solution' first.");
                return;
            }

            while (true)
            {
                if (string.IsNullOrWhiteSpace(entity))
                    entity = Ask("Enter entity name (leave blank for common)");
                if (string.IsNullOrWhiteSpace(entity))
                    break;
                entity = SanitizeIdentifier(entity);
                if (!EnumScaffolder.EntityExists(config, entity))
                {
                    Error($"Entity '{entity}' does not exist.");
                    entity = null;
                    continue;
                }
                break;
            }

            if (string.IsNullOrWhiteSpace(enumName))
                enumName = Ask("Enter enum name");
            if (string.IsNullOrWhiteSpace(enumName))
            {
                Error("Enum name is required.");
                return;
            }
            enumName = SanitizeIdentifier(enumName);
            if (EnumScaffolder.Generate(config, entity, enumName))
            {
                var msg = string.IsNullOrWhiteSpace(entity)
                    ? $"Enum {enumName} generated under Common."
                    : $"Enum {enumName} for {entity} generated.";
                Success(msg);
            }
            return;
        }

        if (args.Length >= 2 && args[0].ToLower() == "new" && args[1].ToLower() == "action")
        {
            string? entity = null;
            string? action = null;
            string? outputPath = null;
            bool? isCommand = null;

            for (int i = 2; i < args.Length; i++)
            {
                if (args[i].StartsWith("--entity="))
                    entity = args[i].Substring("--entity=".Length);
                else if (args[i].StartsWith("--action="))
                    action = args[i].Substring("--action=".Length);
                else if (args[i].StartsWith("--is-command="))
                    isCommand = bool.Parse(args[i].Substring("--is-command=".Length));
                else if (args[i].StartsWith("--output="))
                    outputPath = args[i].Substring("--output=".Length);
            }

            if (string.IsNullOrWhiteSpace(entity))
                entity = Ask("Enter entity name");
            if (string.IsNullOrWhiteSpace(action))
                action = Ask("Enter action name");
            if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(action))
            {
                Error("Entity and action are required.");
                return;
            }

            entity = SanitizeIdentifier(entity);
            action = SanitizeIdentifier(action);

            if (isCommand == null)
                isCommand = AskYesNo("Is command?", true);
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = PathState.Load() ?? Directory.GetCurrentDirectory();

            var basePath = outputPath!;
            var config = ConfigManager.Load(basePath);
            if (config == null)
            {
                Error("Solution configuration not found. Run 'new solution' first.");
                return;
            }

            ActionScaffolder.Generate(config, entity, action, isCommand.Value);
            return;
        }

        if (args.Length >= 2 && args[0].ToLower() == "new" && args[1].ToLower() == "service")
        {
            string? outputPath = null;
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i].StartsWith("--output="))
                    outputPath = args[i].Substring("--output=".Length);
            }

            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = PathState.Load() ?? Directory.GetCurrentDirectory();

            var basePath = outputPath!;
            var config = ConfigManager.Load(basePath);
            if (config == null)
            {
                Error("Solution configuration not found. Run 'new solution' first.");
                return;
            }

            ServiceScaffolder.Generate(config);
            return;
        }

        if (args.Length >= 1 && args[0].ToLower() == "exec")
        {
            string? outputPath = null;
            bool useDocker = false;
            bool detach = false;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].StartsWith("--output="))
                    outputPath = args[i].Substring("--output=".Length);
                else if (args[i] == "--docker")
                    useDocker = true;
                else if (args[i] == "--docker-detach")
                {
                    useDocker = true;
                    detach = true;
                }
            }

            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = PathState.Load() ?? Directory.GetCurrentDirectory();

            var basePath = outputPath!;
            var config = ConfigManager.Load(basePath);
            if (config == null)
            {
                Error("Solution configuration not found. Run 'new solution' first.");
                return;
            }

            var solutionPath = config.SolutionPath;

            // ensure unit of work and repositories exist before syncing project wiring
            foreach (var e in config.Entities.Keys)
                new UnitOfWorkStep().Execute(config, e);

            // keep project wiring (e.g. IUnitOfWork registration) up to date after updates
            new ProjectUpdateStep().Execute(config, string.Empty);

            // run unit of work step again to apply registrations if DI files were recreated
            foreach (var e in config.Entities.Keys)
                new UnitOfWorkStep().Execute(config, e);

            // ensure any pending migrations are applied before running
            UpdateMigrations(config, solutionPath);

            if (useDocker)
            {
                var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "development";
                if (!int.TryParse(config.ApiPort, out var port)) port = 5000;
                if (IsPortInUse(port))
                {
                    Error($"Port {port} is already in use.");
                    return;
                }
                RefreshDockerCompose(solutionPath, config.SolutionName, config.StartupProject, config.ApiPort, env);
                var image = string.IsNullOrWhiteSpace(config.DockerImage) ? $"{config.SolutionName.ToLower()}.api" : config.DockerImage;
                var tag = $"{image}:0.0.0";
                var container = string.IsNullOrWhiteSpace(config.DockerContainer) ? $"{config.SolutionName.ToLower()}-api" : config.DockerContainer;

                bool runCleanup = false;
                bool cleaned = false;
                void Cleanup()
                {
                    if (cleaned || !runCleanup) return;
                    cleaned = true;
                    Step("Docker Cleanup", "Tearing down containers and images");
                    var down = RunCommand("docker compose down", solutionPath);
                    SubStep(down, $"Container stopped: {container}");
                    SubStep(down, $"Container removed: {container}");
                    if (ImageExists(tag))
                    {
                        var rm = RunCommand($"docker rmi {tag}", solutionPath);
                        SubStep(rm, $"Image removed: {tag}");
                    }
                    Logger.Blank();
                }

                ConsoleCancelEventHandler? handler = null;
                if (!detach)
                {
                    handler = (_, e) =>
                    {
                        e.Cancel = true;
                        _cancelRequested = true;
                        try { _currentProcess?.Kill(true); } catch { }
                    };
                    Console.CancelKeyPress += handler;
                }
                try
                {
                    if (ContainerExists(container) || ImageExists(tag))
                    {
                        if (AskYesNo("Existing Docker resources found. Kill and recreate?", true))
                        {
                            Step("Docker Cleanup", "Removing existing resources");
                            if (ContainerExists(container))
                            {
                                var rc = RunCommand($"docker rm -f {container}");
                                SubStep(rc, $"Removed container: {container}");
                            }
                            if (ImageExists(tag))
                            {
                                var ri = RunCommand($"docker rmi {tag}");
                                SubStep(ri, $"Removed image: {tag}");
                            }
                            Logger.Blank();
                        }
                        else
                        {
                            Error("Docker run aborted.");
                            return;
                        }
                    }
                    config.DockerImage = image;
                    config.DockerContainer = container;
                    ConfigManager.Save(solutionPath, config);
                    runCleanup = !detach;
                    Step("Docker Build", $"Building image {tag}");
                    var buildOk = RunCommand($"ASPNETCORE_ENVIRONMENT={env} docker compose build", solutionPath);
                    SubStep(buildOk, $"Image built: {tag}");
                    Logger.Blank();

                    Step("Docker Run", $"Starting container {container}");
                    var createOk = RunCommand($"ASPNETCORE_ENVIRONMENT={env} docker compose create", solutionPath);
                    SubStep(createOk, $"Container created: {container}");
                    var startOk = createOk && RunCommand($"ASPNETCORE_ENVIRONMENT={env} docker compose start", solutionPath);
                    SubStep(startOk, $"Container started: {container}");
                    if (startOk)
                    {
                        var baseUrl = $"http://localhost:{port}";
                        SubStep(true, $"Application running at {baseUrl}");
                        if (env.Equals("development", StringComparison.OrdinalIgnoreCase) ||
                            env.Equals("test", StringComparison.OrdinalIgnoreCase))
                            SubStep(true, $"Swagger UI available at {baseUrl}/swagger/index.html");
                    }
                    Logger.Blank();

                    if (!detach)
                        RunCommand("docker compose logs -f", solutionPath);
                }
                finally
                {
                    if (!detach)
                    {
                        Cleanup();
                        if (handler != null)
                            Console.CancelKeyPress -= handler;
                        _cancelRequested = false;
                    }
                }
            }
            else
            {
                var runProj = $"{config.StartupProject}/{config.StartupProject}.csproj";
                RunProject(runProj, solutionPath);
            }
            return;
        }

        if (args.Length >= 2 && args[0].ToLower() == "remove" && args[1].ToLower() == "migration")
        {
            string? outputPath = null;
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i].StartsWith("--output="))
                    outputPath = args[i].Substring("--output=".Length);
            }

            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = PathState.Load() ?? Directory.GetCurrentDirectory();

            var basePath = outputPath!;
            var config = ConfigManager.Load(basePath);
            if (config == null)
            {
                Error("Solution configuration not found. Run 'new solution' first.");
                return;
            }

            var provider = config.DatabaseProvider;
            if (string.IsNullOrWhiteSpace(provider) || provider.Equals("Mongo", StringComparison.OrdinalIgnoreCase))
            {
                Info("No migrations to remove for the selected provider.");
                return;
            }

            if (!EnsureEfTool(basePath))
                return;

            var infraProj = $"{config.SolutionName}.Infrastructure/{config.SolutionName}.Infrastructure.csproj";
            var startProj = $"{config.StartupProject}/{config.StartupProject}.csproj";
            var migrations = ListMigrations(infraProj, startProj, basePath);
            if (migrations.Length == 0)
            {
                Info("No migrations found.");
                return;
            }
            var prev = migrations.Length > 1 ? migrations[migrations.Length - 2] : "0";
            if (!RunCommand($"dotnet ef database update {prev} --project {infraProj} --startup-project {startProj} --no-build", basePath))
            {
                Error("Failed to rollback database; migration removal aborted.");
                return;
            }
            if (!RunCommand($"dotnet ef migrations remove --force --project {infraProj} --startup-project {startProj} --no-build", basePath))
            {
                Error("Failed to remove migration.");
                return;
            }
            RunCommand("dotnet build", basePath);

            return;
        }

        if (args.Length >= 2 && args[0].ToLower() == "new" && args[1].ToLower() == "solution")
        {
            string? solutionName = null;
            string? outputPath = null;
            string? startup = null;
            string? style = null;
            for (int i = 2; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--"))
                    solutionName = args[i];
                else if (args[i].StartsWith("--output="))
                    outputPath = args[i].Substring("--output=".Length);
                else if (args[i].StartsWith("--startup="))
                    startup = args[i].Substring("--startup=".Length);
                else if (args[i].StartsWith("--style="))
                    style = args[i].Substring("--style=".Length);
            }

            if (string.IsNullOrWhiteSpace(solutionName))
                solutionName = Ask("Enter solution name");
            if (string.IsNullOrWhiteSpace(solutionName))
            {
                Error("Solution name is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = Ask("Output path", Directory.GetCurrentDirectory());
            if (string.IsNullOrWhiteSpace(startup))
                startup = $"{solutionName}.API";
            if (string.IsNullOrWhiteSpace(style))
                style = AskOption("Select API style", new[] { "controller", "fast" }).ToLower();
            if (string.IsNullOrWhiteSpace(style))
                style = "controller";

            GenerateSolution(solutionName, outputPath!, startup!, style!);
            return;
        }

        GenerateSolutionInteractive();
    }

    public static string Ask(string message, string? defaultValue = null)
    {
        Logger.Blank();
        Console.Write($"{message}{(defaultValue != null ? $" [{defaultValue}]" : "")}: ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? (defaultValue ?? string.Empty) : input;
    }

    public static bool AskYesNo(string message, bool defaultYes)
    {
        Logger.Blank();
        var def = defaultYes ? "y" : "n";
        while (true)
        {
            Console.Write($"{message} (y/n) [{def}]: ");
            var input = Console.ReadLine()?.Trim().ToLower();
            if (string.IsNullOrEmpty(input))
                return defaultYes;
            if (input == "y" || input == "yes")
                return true;
            if (input == "n" || input == "no")
                return false;
            Console.WriteLine("Please enter 'y' or 'n'.");
        }
    }

    public static string AskOption(string message, string[] options, int defaultIndex = 0, int[]? disabledIndices = null)
    {
        Logger.Blank();
        Console.WriteLine(message);
        var disabled = disabledIndices != null ? new HashSet<int>(disabledIndices) : new HashSet<int>();

        var index = Math.Clamp(defaultIndex, 0, options.Length - 1);
        if (disabled.Contains(index))
            index = Enumerable.Range(0, options.Length).First(i => !disabled.Contains(i));

        Console.CursorVisible = false;
        while (true)
        {
            for (int i = 0; i < options.Length; i++)
            {
                bool isDisabled = disabled.Contains(i);
                bool isSelected = i == index;
                var prefix = isSelected ? "➤" : " ";
                if (isDisabled)
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                else if (isSelected)
                    Console.ForegroundColor = ConsoleColor.Cyan;

                // keep lines the same length to avoid leftover characters when toggling selection
                Console.WriteLine($"{prefix} {options[i]}");
                Console.ResetColor();
            }

            var key = Console.ReadKey(true).Key;
            if (key == ConsoleKey.UpArrow)
            {
                do
                {
                    index = index == 0 ? options.Length - 1 : index - 1;
                } while (disabled.Contains(index));
            }
            else if (key == ConsoleKey.DownArrow)
            {
                do
                {
                    index = index == options.Length - 1 ? 0 : index + 1;
                } while (disabled.Contains(index));
            }
            else if (key == ConsoleKey.Enter)
            {
                Console.CursorVisible = true;
                Console.SetCursorPosition(0, Console.CursorTop);
                return options[index];
            }

            Console.SetCursorPosition(0, Console.CursorTop - options.Length);
        }
    }

    public static void Info(string title, string? description = null) => Logger.Info(title, description);

    public static void Success(string title, string? description = null) => Logger.Success(title, description);

    public static void Error(string title, string? description = null) => Logger.Error(title, description);

    public static void Step(string title, string? description = null)
    {
        Logger.Blank();
        Logger.Section("🔹", title, description);
    }

    public static void SubStep(bool success, string message) => Logger.SubStep(success, message);

    static void GenerateSolutionInteractive()
    {
        Info("Welcome to ScaffoldCleanArch Tool!");
        var solutionName = Ask("Enter the name of your solution");
        if (string.IsNullOrWhiteSpace(solutionName))
        {
            Error("Solution name cannot be empty!");
            return;
        }

        var outputPath = Ask("Enter output path", Directory.GetCurrentDirectory());
        var startup = $"{solutionName}.API";
        var style = AskOption("Select API style", new[] { "controller", "fast" }).ToLower();
        if (string.IsNullOrWhiteSpace(style)) style = "controller";

        GenerateSolution(solutionName, outputPath, startup, style);
    }

    static void GenerateSolution(string solutionName, string outputPath, string startupProject, string apiStyle)
    {
        var solutionDir = Path.Combine(outputPath, solutionName);
        if (!Directory.Exists(solutionDir))
            Directory.CreateDirectory(solutionDir);

        Directory.SetCurrentDirectory(solutionDir);

        var gitInstalled = IsGitInstalled();
        var gitInitialized = false;
        if (AskYesNo("Initialize git repository?", true))
        {
            EnsureDotnetGitIgnore(solutionDir);
            if (gitInstalled)
            {
                gitInitialized = RunCommand("git init", solutionDir);
                if (gitInitialized)
                    RunCommand("git branch -M main", solutionDir);
            }
            else
            {
                Error("Git is not installed.");
            }
        }

        if (AskYesNo("Create README.md?", true))
            EnsureReadmeTemplate(solutionDir, solutionName);

        RunCommand($"dotnet new sln -n {solutionName} --force");
        RunCommand($"dotnet new classlib -n {solutionName}.Core --force --framework net8.0");
        RunCommand($"dotnet new classlib -n {solutionName}.Application --force --framework net8.0");
        RunCommand($"dotnet new classlib -n {solutionName}.Infrastructure --force --framework net8.0");
        RunCommand($"dotnet new webapi -n {solutionName}.API --force --framework net8.0");
        

        DeleteDefaultClass($"{solutionName}.Core");
        DeleteDefaultClass($"{solutionName}.Application");
        DeleteDefaultClass($"{solutionName}.Infrastructure");

        RunCommand($"dotnet sln add {solutionName}.Core/{solutionName}.Core.csproj");
        RunCommand($"dotnet sln add {solutionName}.Application/{solutionName}.Application.csproj");
        RunCommand($"dotnet sln add {solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj");
        RunCommand($"dotnet sln add {solutionName}.API/{solutionName}.API.csproj");

        RunCommand($"dotnet add {solutionName}.Application/{solutionName}.Application.csproj reference {solutionName}.Core/{solutionName}.Core.csproj");
        RunCommand($"dotnet add {solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj reference {solutionName}.Application/{solutionName}.Application.csproj");
        RunCommand($"dotnet add {solutionName}.API/{solutionName}.API.csproj reference {solutionName}.Application/{solutionName}.Application.csproj");
        RunCommand($"dotnet add {solutionName}.API/{solutionName}.API.csproj reference {solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj");

        var provider = DatabaseProviderSelector.Choose();
        var port = ReadApiPort(solutionDir, startupProject);
        var config = new SolutionConfig { SolutionName = solutionName, SolutionPath = solutionDir, StartupProject = startupProject, DatabaseProvider = provider, ApiStyle = apiStyle, ApiPort = port };
        ConfigManager.Save(solutionDir, config);
        PathState.Save(solutionDir);
        new ApplicationStep().Execute(config, string.Empty);
        new ProjectUpdateStep().Execute(config, string.Empty);

        if (AskYesNo("Add Docker support?", true))
            CreateDockerArtifacts(solutionDir, solutionName, startupProject, port);

        if (gitInstalled && gitInitialized)
        {
            RunCommand("git add .", solutionDir);
            RunCommand("git commit -m \"init\"", solutionDir);
        }

        Console.WriteLine();
        Success("Solution created successfully!");
        Info($"Navigate to the '{solutionName}' directory and run 'dotnet build'.");
    }

    static string ReadApiPort(string basePath, string startupProject)
    {
        var lsPath = Path.Combine(basePath, startupProject, "Properties", "launchSettings.json");
        if (!File.Exists(lsPath))
            return "5000";
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(lsPath));
            var profiles = doc.RootElement.GetProperty("profiles");
            foreach (var prof in profiles.EnumerateObject())
            {
                if (prof.Value.TryGetProperty("applicationUrl", out var urlEl))
                {
                    var url = urlEl.GetString() ?? string.Empty;
                    var http = url.Split(';').FirstOrDefault(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
                    if (http != null && Uri.TryCreate(http, UriKind.Absolute, out var uri))
                        return uri.Port.ToString();
                }
            }
        }
        catch { }
        return "5000";
    }

    static void CreateDockerArtifacts(string basePath, string solutionName, string startupProject, string port)
    {
        WriteDockerfile(basePath, solutionName, startupProject, port);
        RefreshDockerCompose(basePath, solutionName, startupProject, port, "development");
    }

    static void WriteDockerfile(string basePath, string solutionName, string startupProject, string port)
    {
        var dockerfilePath = Path.Combine(basePath, "Dockerfile");
        if (File.Exists(dockerfilePath)) return;
        var nl = Environment.NewLine;
        var content =
            $"FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base{nl}" +
            "WORKDIR /app" + nl +
            $"EXPOSE {port}" + nl +
            nl +
            "FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build" + nl +
            "WORKDIR /src" + nl +
            "COPY . ." + nl +
            $"RUN dotnet restore \"{startupProject}/{startupProject}.csproj\"" + nl +
            $"WORKDIR /src/{startupProject}" + nl +
            $"RUN dotnet publish \"{startupProject}.csproj\" -c Release -o /app/publish /p:UseAppHost=false" + nl +
            nl +
            "FROM base AS final" + nl +
            "WORKDIR /app" + nl +
            "COPY --from=build /app/publish ." + nl +
            $"ENTRYPOINT [\"dotnet\", \"{startupProject}.dll\"]" + nl;
        File.WriteAllText(dockerfilePath, content);
        Success("Dockerfile created.");
    }

    static void RefreshDockerCompose(string basePath, string solutionName, string startupProject, string port, string env)
    {
        var composePath = Path.Combine(basePath, "docker-compose.yml");
        var envPath = Path.Combine(basePath, startupProject, "config", "env", $".env.{env}");
        var envHash = File.Exists(envPath) ? ComputeHash(File.ReadAllText(envPath)) : string.Empty;
        var image = $"{solutionName.ToLower()}.api";
        var container = $"{solutionName.ToLower()}-api";
        var nl = Environment.NewLine;
        var content =
            $"# env-hash:{envHash}{nl}" +
            "services:" + nl +
            "  api:" + nl +
            "    build:" + nl +
            "      context: ." + nl +
            "      dockerfile: Dockerfile" + nl +
            $"    image: {image}:0.0.0{nl}" +
            $"    container_name: {container}{nl}" +
            "    ports:" + nl +
            $"      - \"{port}:{port}\"{nl}" +
            "    environment:" + nl +
            $"      - ASPNETCORE_URLS=http://+:{port}{nl}" +
            $"      - ASPNETCORE_ENVIRONMENT=${{ASPNETCORE_ENVIRONMENT:-{env}}}{nl}" +
            "    env_file:" + nl +
            $"      - {startupProject}/config/env/.env.${{ASPNETCORE_ENVIRONMENT:-{env}}}{nl}";
        if (!File.Exists(composePath) || File.ReadAllText(composePath) != content)
        {
            File.WriteAllText(composePath, content);
            Success("docker-compose.yml updated.");
        }
    }

    static string ComputeHash(string content)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    static bool IsPortInUse(int port)
    {
        try
        {
            var listener = new TcpListener(System.Net.IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return false;
        }
        catch (SocketException)
        {
            return true;
        }
    }

    static bool ContainerExists(string name)
    {
        var (_, output) = RunCommandCapture($"docker ps -a --filter name={name} --format \"{{{{.Names}}}}\"");
        return !string.IsNullOrWhiteSpace(output.Trim());
    }

    static bool ImageExists(string name)
    {
        var (_, output) = RunCommandCapture($"docker images -q {name}");
        return !string.IsNullOrWhiteSpace(output.Trim());
    }

    public static bool RunCommand(string command, string? workingDir = null, bool print = true)
    {
        string shell, shellArgs;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            shell = "cmd.exe";
            shellArgs = $"/c {command}";
        }
        else
        {
            shell = "/bin/bash"; // یا "/bin/zsh" برای مک و لینوکس
            shellArgs = $"-c \"{command}\"";
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = shellArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDir ?? Directory.GetCurrentDirectory()
            }
        };

        var lines = new ConcurrentQueue<string>();
        var outputBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lines.Enqueue(e.Data);
                outputBuilder.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lines.Enqueue(e.Data);
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        _currentProcess = process;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        Task? spinner = null;
        if (print)
            spinner = Task.Run(() => ShowSpinner(process, lines));
        process.WaitForExit();
        spinner?.Wait();
        _currentProcess = null;

        var output = outputBuilder.ToString();
        bool success = process.ExitCode == 0;

        if (_cancelRequested)
            return false;

        if (print)
        {
            if (!success)
                Error(string.IsNullOrWhiteSpace(output) ? "Command failed" : output.Trim());
            else
                Success(command);
        }

        return success;
    }

    public static (bool Success, string Output) RunCommandCapture(string command, string? workingDir = null)
    {
        string shell, shellArgs;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            shell = "cmd.exe";
            shellArgs = $"/c {command}";
        }
        else
        {
            shell = "/bin/bash";
            shellArgs = $"-c \"{command}\"";
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = shellArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDir ?? Directory.GetCurrentDirectory()
            }
        };

        var lines = new ConcurrentQueue<string>();
        var outputBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lines.Enqueue(e.Data);
                outputBuilder.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lines.Enqueue(e.Data);
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        _currentProcess = process;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var spinner = Task.Run(() => ShowSpinner(process, lines));
        process.WaitForExit();
        spinner.Wait();
        _currentProcess = null;

        var output = outputBuilder.ToString();
        bool success = process.ExitCode == 0;
        if (_cancelRequested)
            return (false, output);
        return (success, output);
    }

    static void ShowSpinner(Process process, ConcurrentQueue<string> lines)
    {
        var frames = new[] { '⣾', '⣽', '⣻', '⢿', '⡿', '⣟', '⣯', '⣷' };
        var idx = 0;
        var last = string.Empty;
        var color = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        while (!process.HasExited || !lines.IsEmpty)
        {
            while (lines.TryDequeue(out var line))
                last = line;
            if (last.Length > Console.WindowWidth - 2)
                last = last.Substring(0, Console.WindowWidth - 2);
            Console.Write($"\r{frames[idx++ % frames.Length]} {last}");
            Thread.Sleep(120);
        }
        Console.ForegroundColor = color;
        var width = Console.WindowWidth;
        Console.Write("\r" + new string(' ', width) + "\r");
    }

    public static void RunProject(string project, string basePath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project {project}",
                WorkingDirectory = basePath,
                UseShellExecute = false
            }
        };
        process.Start();
        process.WaitForExit();
    }

    static void UpdateMigrations(SolutionConfig config, string basePath)
    {
        var provider = config.DatabaseProvider;
        if (string.IsNullOrWhiteSpace(provider) || provider.Equals("Mongo", StringComparison.OrdinalIgnoreCase))
        {
            Info("No migrations for the selected provider.");
            return;
        }

        if (!EnsureEfTool(basePath))
            return;

        if (RunCommand("dotnet build", basePath))
        {
            var infraProj = $"{config.SolutionName}.Infrastructure/{config.SolutionName}.Infrastructure.csproj";
            var startProj = $"{config.StartupProject}/{config.StartupProject}.csproj";
            var migName = $"Auto_{DateTime.UtcNow:yyyyMMddHHmmss}";
            var (success, output) = RunCommandCapture($"dotnet ef migrations add {migName} --project {infraProj} --startup-project {startProj} --output-dir {PathConstants.MigrationsRelativePath}", basePath);
            var proceed = true;
            if (!success)
            {
                if (output.Contains("No changes were detected", StringComparison.OrdinalIgnoreCase))
                {
                    Info("No changes were detected.");
                }
                else
                {
                    Error(output.Trim());
                    proceed = false;
                }
            }

            if (proceed)
            {
                var (dbSuccess, dbOutput) = RunCommandCapture($"dotnet ef database update --project {infraProj} --startup-project {startProj}", basePath);
                if (!dbSuccess)
                    Error(dbOutput.Trim());
                else if (dbOutput.Contains("No migrations were applied", StringComparison.OrdinalIgnoreCase))
                    Info("Database already up to date.");
            }
        }
        else
        {
            Error("Build failed; skipping migrations.");
        }
    }

    static string[] ListMigrations(string infraProj, string startProj, string basePath)
    {
        var (success, output) = RunCommandCapture($"dotnet ef migrations list --project {infraProj} --startup-project {startProj} --no-build", basePath);
        if (!success)
            return Array.Empty<string>();
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var regex = new Regex("^\\d+_");
        var list = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!regex.IsMatch(trimmed))
                continue;
            var space = trimmed.IndexOf(' ');
            if (space >= 0)
                trimmed = trimmed.Substring(0, space);
            list.Add(trimmed);
        }
        return list.ToArray();
    }

    static bool IsGitInstalled() => RunCommand("git --version", print: false);

    static void EnsureDotnetGitIgnore(string basePath)
    {
        var gitignorePath = Path.Combine(basePath, ".gitignore");
        if (!File.Exists(gitignorePath))
        {
            if (!RunCommand("dotnet new gitignore", basePath))
            {
                var lines = new[]
                {
                    "# Build Folders",
                    "bin/",
                    "obj/",
                    "publish/",
                    "",
                    "# User-specific files",
                    "*.rsuser",
                    "*.suo",
                    "*.user",
                    "*.userosscache",
                    "*.sln.docstates",
                    "",
                    "# Visual Studio",
                    ".vs/"
                };
                File.WriteAllLines(gitignorePath, lines);
            }
            Success(".gitignore file added.");
        }
    }

    static void EnsureReadmeTemplate(string basePath, string solutionName)
    {
        var readmePath = Path.Combine(basePath, "README.md");
        if (File.Exists(readmePath))
            return;

        var content =
            $"# {solutionName}\n\n" +
            "A brief description of your project.\n\n" +
            "## Table of Contents\n\n" +
            "- [Features](#features)\n" +
            "- [Getting Started](#getting-started)\n" +
            "  - [Prerequisites](#prerequisites)\n" +
            "  - [Installation](#installation)\n" +
            "  - [Usage](#usage)\n" +
            "- [Contributing](#contributing)\n" +
            "- [License](#license)\n" +
            "- [Acknowledgements](#acknowledgements)\n\n" +
            "## Features\n\n" +
            "- Describe the features of your project.\n\n" +
            "## Getting Started\n\n" +
            "### Prerequisites\n\n" +
            "- List prerequisites here.\n\n" +
            "### Installation\n\n" +
            "```bash\n# Installation steps\n```\n\n" +
            "### Usage\n\n" +
            "```bash\n# Usage example\n```\n\n" +
            "## Contributing\n\n" +
            "Describe how to contribute.\n\n" +
            "## License\n\n" +
            "Specify the license.\n\n" +
            "## Acknowledgements\n\n" +
            "- List acknowledgements.\n";
        File.WriteAllText(readmePath, content);
        Success("README.md file added.");
    }

    public static bool EnsureEfTool(string? workingDir = null)
    {
        if (RunCommand("dotnet ef --version", workingDir, print: false))
            return true;

        Info("dotnet-ef not found. Attempting installation...");
        var cmd = GetEfToolInstallMessage();
        if (RunCommand(cmd, workingDir))
            return RunCommand("dotnet ef --version", workingDir, print: false);

        Error($"Failed to install dotnet-ef. Install manually with: {cmd}");
        return false;
    }

    public static bool EnsureDotnetSdk()
    {
        if (RunCommand("dotnet --version", print: false))
            return true;

        Error(".NET SDK not found. Install from https://dotnet.microsoft.com/download");
        return false;
    }

    public static string GetEfToolInstallMessage()
    {
        const string version = "8.*";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"dotnet tool install --global dotnet-ef --version {version} && setx PATH \"%PATH%;%USERPROFILE%\\.dotnet\\tools\"";
        else
            return $"dotnet tool install --global dotnet-ef --version {version} && export PATH=\"$PATH:$HOME/.dotnet/tools\"";
    }

    static string SanitizeIdentifier(string value) =>
        Regex.Replace(value ?? string.Empty, @"[^A-Za-z0-9_]", "");

    static void DeleteDefaultClass(string projectName)
    {
        var filePath = Path.Combine(projectName, "Class1.cs");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Info("Deleted default class", filePath);
        }
    }
}
