using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

partial class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("==========================================");
        Console.WriteLine("🚀 Welcome to ScaffoldCleanArch Tool! 🚀");
        Console.WriteLine("==========================================");
        Console.WriteLine("A powerful solution scaffolding tool for Clean Architecture!");
        Console.WriteLine("🔹 Generates a solution structure with core layers");
        Console.WriteLine("🔹 Adds references between projects automatically");
        Console.WriteLine("🔹 Ready to start coding your dream project!");
        Console.WriteLine("==========================================\n");

        if (args.Length == 0)
        {
            CreateSolution();
            return;
        }

        var command = args[0].ToLower();
        Console.WriteLine($"Command: {command}");
        switch (command)
        {

            case "add-packages":
                InstallPackages(args[1]);
                break;

            default:
                Console.WriteLine($"Unknown command: {command}");
                break;
        }


    }

    static void CreateSolution()
    {
        Console.Write("Enter the name of your solution: ");
        var solutionName = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(solutionName))
        {
            Console.WriteLine("Solution name cannot be empty!");
            return;
        }

        // foreach (var package in packages)
        // {
        //     Console.WriteLine($"{package.Key}: (yes/no)");
        //     var input = Console.ReadLine()?.Trim().ToLower();
        //     packages[package.Key].IsAdd = input;
        // }

        // Create the solution folder
        if (!Directory.Exists(solutionName))
        {
            Directory.CreateDirectory(solutionName);
        }

        // Change working directory to the solution folder
        Directory.SetCurrentDirectory(solutionName);



        // Run `dotnet new` commands
        RunCommand($"dotnet new sln -n {solutionName}");
        RunCommand($"dotnet new classlib -n {solutionName}.Core");
        RunCommand($"dotnet new classlib -n {solutionName}.Application");
        RunCommand($"dotnet new classlib -n {solutionName}.Infrastructure");
        RunCommand($"dotnet new webapi -n {solutionName}.API");


        // if (installMediatR == "yes")
        // {
        //     RunCommand($"dotnet add {solutionName}.Application/{solutionName}.Application.csproj package MediatR");
        //     RunCommand($"dotnet add {solutionName}.API/{solutionName}.API.csproj package MediatR.Extensions.Microsoft.DependencyInjection");
        // }

        // if (installFluentValidation == "yes")
        // {
        //     RunCommand($"dotnet add {solutionName}.Application/{solutionName}.Application.csproj package FluentValidation");
        //     RunCommand($"dotnet add {solutionName}.API/{solutionName}.API.csproj package FluentValidation.AspNetCore");
        // }


        // Remove default classes
        DeleteDefaultClass($"{solutionName}.Core");
        DeleteDefaultClass($"{solutionName}.Application");
        DeleteDefaultClass($"{solutionName}.Infrastructure");

        // Add projects to the solution
        RunCommand($"dotnet sln add {solutionName}.Core/{solutionName}.Core.csproj");
        RunCommand($"dotnet sln add {solutionName}.Application/{solutionName}.Application.csproj");
        RunCommand($"dotnet sln add {solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj");
        RunCommand($"dotnet sln add {solutionName}.API/{solutionName}.API.csproj");

        // Add references between projects
        RunCommand($"dotnet add {solutionName}.Application/{solutionName}.Application.csproj reference {solutionName}.Core/{solutionName}.Core.csproj");
        RunCommand($"dotnet add {solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj reference {solutionName}.Application/{solutionName}.Application.csproj");
        RunCommand($"dotnet add {solutionName}.API/{solutionName}.API.csproj reference {solutionName}.Application/{solutionName}.Application.csproj");
        RunCommand($"dotnet add {solutionName}.API/{solutionName}.API.csproj reference {solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj");


        string projectPath = $""; // مسیر پروژه ایجاد شده
        string packagesYamlName = "packages.yml";
        string yamlFilePath = Path.Combine(projectPath, packagesYamlName);

        // محتوای فایل YAML
        string yamlContent = @"
# لیست پکیج‌ها برای نصب
packages:
  - name: ""Swashbuckle.AspNetCore""
    version: ""6.6.0""
    layer: ""API""
    install_commands: 
        - ""dotnet add {solutionName}.{layer}/{solutionName}.{layer}.csproj package {name} --version {version}""
    enabled: true 
".Replace("{solutionName}", solutionName);

        // ایجاد فایل YAML
        File.WriteAllText(yamlFilePath, yamlContent);

        Console.WriteLine($"File 'packages.yml' created at: {yamlFilePath}");



        Console.WriteLine("\n✅ Solution created successfully!");
        Console.WriteLine("==========================================");
        Console.WriteLine($"🌟 Navigate to the '{solutionName}' directory to explore your project.");
        Console.WriteLine($"💻 Run 'dotnet build' to build the solution.");
        Console.WriteLine($"🎉 Start coding your Clean Architecture project now!");
        Console.WriteLine("==========================================");
    }

    static void InstallPackages(string projectName)
    {
        string yamlFilePath = Path.Combine(Directory.GetCurrentDirectory(), $"{projectName}/packages.yml");

        if (!File.Exists(yamlFilePath))
        {
            Console.WriteLine("YAML file not found. Please ensure 'packages.yml' exists in the solution directory.");
            return;
        }

        var yamlContent = File.ReadAllText(yamlFilePath);
        var packages = ParseYaml(yamlContent);

        foreach (var package in packages)
        {
            if (package.Enabled)
            {
                foreach (var command in package.InstallCommands)
                {
                    RunCommand(command);
                }
                Console.WriteLine($"Installed package '{package.Name}' in layer '{package.Layer}'.");
            }
        }
    }

    static List<Package> ParseYaml(string yamlFilePath = "packages.yml")
    {

        if (!File.Exists(yamlFilePath))
        {
            Console.WriteLine($"File '{yamlFilePath}' not found.");
            return default;
        }

        // خواندن فایل YAML
        string yamlContent = File.ReadAllText(yamlFilePath);

        // پارس فایل YAML
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance) // پشتیبانی از سبک نام‌گذاری camelCase
            .Build();

        var config = deserializer.Deserialize<List<Package>>(yamlContent);
        return config;
    }

    class Package
    {
        public string Name { get; set; }
        public string? Version { get; set; } = "";
        public string Layer { get; set; }
        public List<string> InstallCommands { get; set; }
        public bool Enabled { get; set; }
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