using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length >= 3 && args[0].ToLower() == "new" && args[1].ToLower() == "crud")
        {
            var solution = args[2];
            var entity = args.Length >= 4 ? args[3] : null;
            if (string.IsNullOrWhiteSpace(solution) || string.IsNullOrWhiteSpace(entity))
            {
                Console.WriteLine("Usage: dotnet-arch new crud <SolutionName> <EntityName>");
                return;
            }

            CrudScaffolder.Generate(solution, entity);
            return;
        }

        GenerateSolution();
    }

    static void GenerateSolution()
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

        if (!Directory.Exists(solutionName))
        {
            Directory.CreateDirectory(solutionName);
        }

        Directory.SetCurrentDirectory(solutionName);

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