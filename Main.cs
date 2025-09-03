using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNetArch.Scaffolding;
using DotNetArch.Scaffolding.Steps;

class Program
{
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
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].StartsWith("--output="))
                    outputPath = args[i].Substring("--output=".Length);
                else if (args[i] == "--docker")
                    useDocker = true;
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

            // ensure unit of work and repositories exist before syncing project wiring
            foreach (var e in config.Entities.Keys)
                new UnitOfWorkStep().Execute(config, e);

            // keep project wiring (e.g. IUnitOfWork registration) up to date after updates
            new ProjectUpdateStep().Execute(config, string.Empty);

            // run unit of work step again to apply registrations if DI files were recreated
            foreach (var e in config.Entities.Keys)
                new UnitOfWorkStep().Execute(config, e);

            // ensure any pending migrations are applied before running
            UpdateMigrations(config, basePath);

            var runProj = $"{config.StartupProject}/{config.StartupProject}.csproj";
            if (useDocker)
                RunDocker(config, basePath);
            else
                RunProject(runProj, basePath);
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
        Console.Write($"{message}{(defaultValue != null ? $" [{defaultValue}]" : "")}: ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? (defaultValue ?? string.Empty) : input;
    }

    public static bool AskYesNo(string message, bool defaultYes)
    {
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

    public static string AskOption(string message, string[] options, int defaultIndex = 0)
    {
        Console.WriteLine(message);
        for (int i = 0; i < options.Length; i++)
            Console.WriteLine($"{i + 1} - {options[i]}");
        Console.Write($"Your choice [{defaultIndex + 1}]: ");
        var input = Console.ReadLine();
        return int.TryParse(input, out var idx) && idx >= 1 && idx <= options.Length
            ? options[idx - 1]
            : options[defaultIndex];
    }

    public static void Info(string msg)
    {
        Console.WriteLine($"ℹ️ {msg}");
        Console.WriteLine();
    }

    public static void Success(string msg)
    {
        Console.WriteLine($"✅ {msg}");
        Console.WriteLine();
    }

    public static void Error(string msg)
    {
        Console.WriteLine($"❌ {msg}");
        Console.WriteLine();
    }

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

        var initGit = AskYesNo("Initialize git repository?", true);

        EnsureDocker();
        EnsureDockerCompose();

        Directory.SetCurrentDirectory(solutionDir);

        RunCommand($"dotnet new sln -n {solutionName} --force");
        RunCommand($"dotnet new classlib -n {solutionName}.Core --force");
        RunCommand($"dotnet new classlib -n {solutionName}.Application --force");
        RunCommand($"dotnet new classlib -n {solutionName}.Infrastructure --force");
        RunCommand($"dotnet new webapi -n {solutionName}.API --force");
        

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
        var config = new SolutionConfig { SolutionName = solutionName, SolutionPath = solutionDir, StartupProject = startupProject, DatabaseProvider = provider, ApiStyle = apiStyle };
        ConfigManager.Save(solutionDir, config);
        PathState.Save(solutionDir);
        CreateDockerArtifacts(config);
        new ApplicationStep().Execute(config, string.Empty);
        new ProjectUpdateStep().Execute(config, string.Empty);

        if (initGit && EnsureGit())
        {
            RunCommand("git init", solutionDir);
            RunCommand("dotnet new gitignore", solutionDir);
        }

        Console.WriteLine();
        Success("Solution created successfully!");
        Info($"Navigate to the '{solutionName}' directory and run 'dotnet build'.");
    }

    static void CreateDockerArtifacts(SolutionConfig config)
    {
        var dockerfile = Path.Combine(config.SolutionPath, "Dockerfile");
        var compose = Path.Combine(config.SolutionPath, "docker-compose.yml");
        var env = Path.Combine(config.SolutionPath, ".env");

        var image = config.SolutionName.ToLower() + "-api";
        var version = GetProjectVersion(config);
        var port = GetApiPort(config);

        var dockerContent = $"FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base\nWORKDIR /app\nEXPOSE {port}\n\nFROM mcr.microsoft.com/dotnet/sdk:9.0 AS build\nWORKDIR /src\nCOPY . .\nRUN dotnet publish {config.StartupProject}/{config.StartupProject}.csproj -c Release -o /app/publish\n\nFROM base AS final\nWORKDIR /app\nCOPY --from=build /app/publish .\nENTRYPOINT [\\\"dotnet\\\", \\\"{config.StartupProject}.dll\\\"]\n";
        File.WriteAllText(dockerfile, dockerContent);

        var composeContent = $"services:\n  api:\n    build:\n      context: .\n      dockerfile: Dockerfile\n    image: {image}:{version}\n    container_name: {image}\n    env_file:\n      - .env\n    ports:\n      - \\\"{port}:{port}\\\"\n";
        File.WriteAllText(compose, composeContent);

        if (!File.Exists(env))
        File.WriteAllText(env, "ASPNETCORE_ENVIRONMENT=Development\n");
    }

    static Action StartProgress(string message)
    {
        var spinner = new[] { '|', '/', '-', '\\' };
        var index = 0;
        var active = true;
        var task = Task.Run(() =>
        {
            while (active)
            {
                Console.Write($"\r{spinner[index++ % spinner.Length]} {message}");
                Thread.Sleep(100);
            }
        });
        return () =>
        {
            active = false;
            task.Wait();
            Console.Write($"\r{new string(' ', message.Length + 2)}\r");
        };
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

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

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

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        Action? stop = null;
        if (print)
            stop = StartProgress(command);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        stop?.Invoke();

        bool success = process.ExitCode == 0;

        if (print)
        {
            if (!success)
            {
                var msg = stderr.Length == 0 ? stdout.ToString() : stderr.ToString();
                Error(string.IsNullOrWhiteSpace(msg) ? "Command failed" : msg.Trim());
            }
            else
            {
                Success(command);
            }
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

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

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

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        var stop = StartProgress(command);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        stop();

        bool success = process.ExitCode == 0;
        var output = stderr.Length == 0 ? stdout.ToString() : stdout.Append(stderr).ToString();
        return (success, output);
    }

    static void RunDocker(SolutionConfig config, string basePath)
    {
        if (!EnsureDocker() || !EnsureDockerCompose())
            return;

        CreateDockerArtifacts(config);

        var composePath = Path.Combine(basePath, "docker-compose.yml");
        if (!File.Exists(composePath))
        {
            Error("docker-compose.yml not found.");
            return;
        }

        var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                  Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                  "Development";
        var envFile = $".env.{env}";
        var envPath = Path.Combine(basePath, envFile);
        if (!File.Exists(envPath))
            envPath = Path.Combine(basePath, ".env");

        UpdateComposeEnvFile(composePath, envPath);

        var image = $"{config.SolutionName.ToLower()}-api";
        var version = GetProjectVersion(config);
        var imageWithTag = $"{image}:{version}";
        var state = DockerState.Load();
        if (!string.IsNullOrWhiteSpace(state.LastContainer))
            RunCommand($"docker rm -f {state.LastContainer}", basePath, print: false);
        if (!string.IsNullOrWhiteSpace(state.LastImage))
            RunCommand($"docker rmi {state.LastImage}", basePath, print: false);

        RunCommand("docker compose down", basePath, print: false);

        DockerState.Save(new DockerStateData { LastContainer = image, LastImage = imageWithTag });

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            RunCommand("docker compose down", basePath, print: false);
            RunCommand($"docker rmi {imageWithTag}", basePath, print: false);
            Environment.Exit(0);
        };

        RunCommand("docker compose up --build", basePath);
        RunCommand("docker compose down", basePath, print: false);
        RunCommand($"docker rmi {imageWithTag}", basePath, print: false);
    }

    static string GetProjectVersion(SolutionConfig config)
    {
        var csproj = Path.Combine(config.SolutionPath, $"{config.StartupProject}/{config.StartupProject}.csproj");
        if (File.Exists(csproj))
        {
            var text = File.ReadAllText(csproj);
            var match = Regex.Match(text, "<Version>(.*?)</Version>", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value.Trim();
        }
        return "latest";
    }

    static string GetApiPort(SolutionConfig config)
    {
        var launchSettings = Path.Combine(config.SolutionPath, $"{config.StartupProject}/Properties/launchSettings.json");
        if (File.Exists(launchSettings))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(launchSettings));
                if (doc.RootElement.TryGetProperty("profiles", out var profiles))
                {
                    foreach (var profile in profiles.EnumerateObject())
                    {
                        if (profile.Value.TryGetProperty("applicationUrl", out var url))
                        {
                            var urls = url.GetString()?.Split(';') ?? Array.Empty<string>();
                            foreach (var u in urls)
                            {
                                var m = Regex.Match(u, @":(\\d+)");
                                if (m.Success)
                                    return m.Groups[1].Value;
                            }
                        }
                    }
                }
            }
            catch { }
        }
        return "8080";
    }

    static void UpdateComposeEnvFile(string composePath, string envPath)
    {
        var lines = File.ReadAllLines(composePath).ToList();
        var idx = lines.FindIndex(l => l.TrimStart().StartsWith("env_file"));
        if (idx >= 0 && idx + 1 < lines.Count)
        {
            lines[idx + 1] = "      - " + Path.GetFileName(envPath);
            File.WriteAllLines(composePath, lines);
        }
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
            Console.WriteLine("❌ Build failed; skipping migrations.");
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

    public static bool EnsureEfTool(string? workingDir = null)
    {
        if (RunCommand("dotnet ef --version", workingDir, print: false))
            return true;

        Console.WriteLine("ℹ️ dotnet-ef not found. Attempting installation...");
        var cmd = GetEfToolInstallMessage();
        if (RunCommand(cmd, workingDir))
            return RunCommand("dotnet ef --version", workingDir, print: false);

        Console.WriteLine($"❌ Failed to install dotnet-ef. Install manually with: {cmd}");
        return false;
    }

    public static bool EnsureDotnetSdk()
    {
        if (RunCommand("dotnet --version", print: false))
            return true;

        Console.WriteLine("❌ .NET SDK not found. Install from https://dotnet.microsoft.com/download");
        return false;
    }

    public static string GetEfToolInstallMessage()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "dotnet tool install --global dotnet-ef && setx PATH \"%PATH%;%USERPROFILE%\\.dotnet\\tools\"";
        else
            return "dotnet tool install --global dotnet-ef && export PATH=\"$PATH:$HOME/.dotnet/tools\"";
    }

    static bool EnsureGit()
    {
        if (RunCommand("git --version", print: false))
            return true;

        if (!AskYesNo("Git is not installed. Install it?", false))
            return false;

        string cmd;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (RunCommand("choco --version", print: false))
                cmd = "choco install git -y";
            else if (RunCommand("winget --version", print: false))
                cmd = "winget install -e --id Git.Git --accept-package-agreements --accept-source-agreements --silent";
            else
            {
                Error("No package manager found to install Git. Please install Git manually.");
                return false;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            cmd = "brew install git";
        else
            cmd = "apt-get update && apt-get install -y git";

        return RunCommand(cmd);
    }

    static bool EnsureDocker()
    {
        if (!RunCommand("docker --version", print: false))
        {
            if (!AskYesNo("Docker is not installed. Install it?", false))
                return false;
            if (!InstallDocker())
                return false;
        }

        if (RunCommand("docker ps", print: false))
            return true;

        if (StartDockerDaemon())
            return true;

        Error("Docker is installed but the daemon is not running or the installation is corrupted.");
        if (!AskYesNo("Reinstall Docker? Existing images and containers will be preserved.", false))
            return false;
        if (!UninstallDocker() || !InstallDocker())
            return false;

        return StartDockerDaemon();
    }

    static bool InstallDocker()
    {
        string cmd;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (RunCommand("choco --version", print: false))
                cmd = "choco install docker-desktop -y";
            else if (RunCommand("winget --version", print: false))
                cmd = "winget install -e --id Docker.DockerDesktop --accept-package-agreements --accept-source-agreements --silent";
            else
            {
                Error("No package manager found to install Docker. Please install Docker Desktop manually.");
                return false;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            cmd = "brew install --cask docker";
        else
            cmd = "curl -fsSL https://get.docker.com | sh";

        return RunCommand(cmd);
    }

    static bool UninstallDocker()
    {
        string cmd;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (RunCommand("choco --version", print: false))
                cmd = "choco uninstall docker-desktop -y";
            else if (RunCommand("winget --version", print: false))
                cmd = "winget uninstall -e --id Docker.DockerDesktop --accept-source-agreements --silent";
            else
            {
                Error("No package manager found to uninstall Docker. Remove it manually and rerun the command.");
                return false;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            cmd = "brew uninstall --cask docker";
        else
            cmd = "apt-get remove -y docker docker-engine docker.io containerd runc";

        return RunCommand(cmd);
    }

    static bool StartDockerDaemon()
    {
        string cmd;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            cmd = "powershell -Command \"Start-Process 'Docker Desktop'\"";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            cmd = "open -g -a Docker";
        else
            cmd = "systemctl start docker";

        RunCommand(cmd, print: false);

        for (int i = 0; i < 30; i++)
        {
            if (RunCommand("docker ps", print: false))
                return true;
            Thread.Sleep(1000);
        }

        return false;
    }

    static bool EnsureDockerCompose()
    {
        if (RunCommand("docker compose version", print: false))
            return true;

        StartDockerDaemon();
        if (RunCommand("docker compose version", print: false))
            return true;

        if (!AskYesNo("Docker Compose is not installed. Install it?", false))
            return false;

        string cmd;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (RunCommand("choco --version", print: false))
                cmd = "choco upgrade docker-desktop -y";
            else if (RunCommand("winget --version", print: false))
                cmd = "winget install -e --id Docker.DockerDesktop --accept-package-agreements --accept-source-agreements --silent";
            else
            {
                Error("Please install Docker Desktop manually from https://www.docker.com/products/docker-desktop");
                return false;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            cmd = "brew install docker-compose";
        else
            cmd = "apt-get update && apt-get install -y docker-compose";

        if (!RunCommand(cmd))
            return false;

        StartDockerDaemon();
        return RunCommand("docker compose version", print: false);
    }

    static string SanitizeIdentifier(string value) =>
        Regex.Replace(value ?? string.Empty, @"[^A-Za-z0-9_]", "");

    static void DeleteDefaultClass(string projectName)
    {
        var filePath = Path.Combine(projectName, "Class1.cs");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Console.WriteLine($"🗑️ Deleted default class: {filePath}");
        }
    }
}
