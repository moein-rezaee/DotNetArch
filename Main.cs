using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

class Program
{
    static void Main(string[] args)
    {
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
            {
                Console.WriteLine("Usage: dotnet-arch new crud --entity=EntityName [--output=Path]");
                return;
            }

            var basePath = string.IsNullOrWhiteSpace(outputPath) ? Directory.GetCurrentDirectory() : outputPath;
            var config = ConfigManager.Load(basePath);
            if (config == null)
            {
                Console.WriteLine("Solution configuration not found. Run 'new solution' first.");
                return;
            }

            CrudScaffolder.Generate(config, entity);
            return;
        }

        if (args.Length >= 2 && args[0].ToLower() == "new" && args[1].ToLower() == "solution")
        {
            string? solutionName = null;
            string? outputPath = null;
            string? startup = null;
            for (int i = 2; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--"))
                    solutionName = args[i];
                else if (args[i].StartsWith("--output="))
                    outputPath = args[i].Substring("--output=".Length);
                else if (args[i].StartsWith("--startup="))
                    startup = args[i].Substring("--startup=".Length);
            }

            if (string.IsNullOrWhiteSpace(solutionName))
            {
                Console.WriteLine("Usage: dotnet-arch new solution <SolutionName> [--output=Path] [--startup=ProjectName]");
                return;
            }

            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = Directory.GetCurrentDirectory();
            if (string.IsNullOrWhiteSpace(startup))
                startup = $"{solutionName}.API";

            GenerateSolution(solutionName, outputPath, startup);
            return;
        }

        GenerateSolutionInteractive();
    }

    static void GenerateSolutionInteractive()
    {
        Console.WriteLine("==========================================");
        Console.WriteLine("🚀 Welcome to ScaffoldCleanArch Tool! 🚀");
        Console.WriteLine("==========================================");
        Console.WriteLine("A powerful solution scaffolding tool for Clean Architecture!");
        Console.WriteLine("🔹 Generates a solution structure with core layers");
        Console.WriteLine("🔹 Adds references between projects automatically");
        Console.WriteLine("🔹 Ready to start coding your dream project!");
        Console.WriteLine("==========================================\n");

        Console.Write("Enter the name of your solution: ");
        var solutionName = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(solutionName))
        {
            Console.WriteLine("Solution name cannot be empty!");
            return;
        }

        Console.Write("Enter output path (leave empty for current): ");
        var outputPath = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(outputPath))
            outputPath = Directory.GetCurrentDirectory();

        Console.Write($"Enter startup project name (default {solutionName}.API): ");
        var startup = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(startup))
            startup = $"{solutionName}.API";

        GenerateSolution(solutionName, outputPath, startup);
    }

    static void GenerateSolution(string solutionName, string outputPath, string startupProject)
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

        ConfigManager.Save(solutionDir, new SolutionConfig { SolutionName = solutionName, SolutionPath = solutionDir, StartupProject = startupProject });

        Console.WriteLine("\n✅ Solution created successfully!");
        Console.WriteLine("==========================================");
        Console.WriteLine($"🌟 Navigate to the '{solutionName}' directory to explore your project.");
        Console.WriteLine($"💻 Run 'dotnet build' to build the solution.");
        Console.WriteLine($"🎉 Start coding your Clean Architecture project now!");
        Console.WriteLine("==========================================");
    }
    static void RunCommand(string command)
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
            }
        };

        process.Start();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            Console.WriteLine($"❌ Error: {process.StandardError.ReadToEnd()}");
        }
        else
        {
            Console.WriteLine($"✅ {process.StandardOutput.ReadToEnd()}");
        }
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