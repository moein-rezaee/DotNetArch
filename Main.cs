using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
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

        if (args.Length >= 1 && args[0].ToLower() == "exec")
        {
            string? outputPath = null;
            for (int i = 1; i < args.Length; i++)
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

            var checkMigrations = AskYesNo(
                "Check for entity changes and apply migrations before running?",
                false);
            if (checkMigrations)
                UpdateMigrations(config, basePath);

            var runProj = $"{config.StartupProject}/{config.StartupProject}.csproj";
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

    static string Ask(string message, string? defaultValue = null)
    {
        Console.Write($"{message}{(defaultValue != null ? $" [{defaultValue}]" : "")}: ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? (defaultValue ?? string.Empty) : input;
    }

    static bool AskYesNo(string message, bool defaultYes)
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

    static string AskOption(string message, string[] options, int defaultIndex = 0)
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

    static void Info(string msg)
    {
        Console.WriteLine($"ℹ️ {msg}");
        Console.WriteLine();
    }

    static void Success(string msg)
    {
        Console.WriteLine($"✅ {msg}");
        Console.WriteLine();
    }

    static void Error(string msg)
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

        Directory.SetCurrentDirectory(solutionDir);

        RunCommand($"dotnet new sln -n {solutionName}");
        RunCommand($"dotnet new classlib -n {solutionName}.Core");
        RunCommand($"dotnet new classlib -n {solutionName}.Application");
        RunCommand($"dotnet new classlib -n {solutionName}.Infrastructure");
        RunCommand($"dotnet new webapi -n {solutionName}.API");
        

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
        new ApplicationStep().Execute(config, string.Empty);
        new ProjectUpdateStep().Execute(config, string.Empty);

        Console.WriteLine();
        Success("Solution created successfully!");
        Info($"Navigate to the '{solutionName}' directory and run 'dotnet build'.");
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

        process.Start();
        process.WaitForExit();

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        bool success = process.ExitCode == 0;

        if (print)
        {
            if (!success)
            {
                var msg = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
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

        process.Start();
        process.WaitForExit();

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        bool success = process.ExitCode == 0;
        var output = string.IsNullOrWhiteSpace(stderr) ? stdout : stdout + stderr;
        return (success, output);
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
            var (success, output) = RunCommandCapture($"dotnet ef migrations add {migName} --project {infraProj} --startup-project {startProj} --output-dir Migrations", basePath);
            if (success)
            {
                RunCommand($"dotnet ef database update --project {infraProj} --startup-project {startProj}", basePath);
            }
            else if (output.Contains("No changes were detected", StringComparison.OrdinalIgnoreCase))
            {
                Info("No changes were detected.");
            }
            else
            {
                Error(output.Trim());
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