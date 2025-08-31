using System;
using System.IO;
using DotNetArch.Scaffolding;
using DotNetArch.Scaffolding.Steps;

static class CrudScaffolder
{
    public static void Generate(SolutionConfig config, string entityName)
    {
        if (string.IsNullOrWhiteSpace(config.SolutionName) || string.IsNullOrWhiteSpace(entityName))
        {
            Console.WriteLine("Solution and entity names are required.");
            return;
        }

        var provider = config.DatabaseProvider;
        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = DatabaseProviderSelector.Choose();
            config.DatabaseProvider = provider;
            ConfigManager.Save(config.SolutionPath, config);
        }
        var steps = new IScaffoldStep[]
        {
            new ProjectUpdateStep(),
            new EntityStep(),
            new DbContextStep(),
            new RepositoryStep(),
            new UnitOfWorkStep(),
            new ApplicationStep(),
            new ControllerStep()
        };

        foreach (var step in steps)
            step.Execute(config.SolutionName, entityName, provider, config.SolutionPath, config.StartupProject);

        var prev = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(config.SolutionPath);
            if (Program.RunCommand("dotnet ef --version", config.SolutionPath, print: false))
            {
                var infraProj = $"{config.SolutionName}.Infrastructure/{config.SolutionName}.Infrastructure.csproj";
                var startProj = $"{config.StartupProject}/{config.StartupProject}.csproj";
                var migName = $"Auto_{entityName}_{DateTime.UtcNow:yyyyMMddHHmmss}";
                Program.RunCommand($"dotnet ef migrations add {migName} --project {infraProj} --startup-project {startProj} --output-dir Migrations", config.SolutionPath);
                Program.RunCommand($"dotnet ef database update --project {infraProj} --startup-project {startProj}", config.SolutionPath);
            }
            else
            {
                Console.WriteLine($"⚠️ dotnet-ef not found; skipping migrations. Install with: {Program.GetEfToolInstallMessage()}");
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(prev);
        }

        Console.WriteLine($"CRUD for {entityName} generated using {provider} provider.");
    }
}
