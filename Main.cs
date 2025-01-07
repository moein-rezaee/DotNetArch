namespace DotNetArch;

using System;
using System.IO;
using System.Text;

using System;
using System.Diagnostics;

class Program
{
    static void Main(string[] args)
    {
        Console.Write("Enter project name: ");
        var projectName = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(projectName))
        {
            Console.WriteLine("Project name cannot be empty.");
            return;
        }

        try
        {
            // Create solution
            RunCommand($"dotnet new sln -n {projectName}");

            // Create layers
            var coreProject = $"{projectName}.Core";
            var applicationProject = $"{projectName}.Application";
            var infrastructureProject = $"{projectName}.Infrastructure";
            var apiProject = $"{projectName}.API";

            CreateLayer(coreProject, "classlib", projectName);
            CreateLayer(applicationProject, "classlib", projectName);
            CreateLayer(infrastructureProject, "classlib", projectName);
            CreateLayer(apiProject, "webapi", projectName);

            // Add references
            RunCommand($"dotnet add {applicationProject} reference {coreProject}");
            RunCommand($"dotnet add {infrastructureProject} reference {applicationProject}");
            RunCommand($"dotnet add {apiProject} reference {applicationProject}");
            RunCommand($"dotnet add {apiProject} reference {infrastructureProject}");

            Console.WriteLine($"Project '{projectName}' with Clean Architecture structure created successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static void CreateLayer(string projectName, string template, string solutionName)
    {
        // Create project
        RunCommand($"dotnet new {template} -n {projectName}");
        // Add to solution
        RunCommand($"dotnet sln {solutionName}.sln add {projectName}");
    }

    static void RunCommand(string command)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string result = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrEmpty(error))
        {
            throw new Exception(error);
        }

        Console.WriteLine(result);
    }
}