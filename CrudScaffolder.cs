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

        if (config.Entities.TryGetValue(entityName, out var existing) && existing.HasCrud)
        {
            Console.WriteLine($"CRUD for {entityName} already exists.");
            return;
        }

        var provider = config.DatabaseProvider;
        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = DatabaseProviderSelector.Choose();
            config.DatabaseProvider = provider;
            ConfigManager.Save(config.SolutionPath, config);
        }

        if (!provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
        {
            if (!Program.EnsureEfTool(config.SolutionPath))
            {
                Console.WriteLine("❌ dotnet-ef installation failed; CRUD generation canceled.");
                return;
            }
        }
        var controllerStep = config.ApiStyle.Equals("fast", StringComparison.OrdinalIgnoreCase)
            ? (IScaffoldStep)new MinimalApiStep()
            : new ControllerStep();

        var steps = new IScaffoldStep[]
        {
            new ProjectUpdateStep(),
            new EntityStep(),
            new DbContextStep(),
            new RepositoryStep(),
            new UnitOfWorkStep(),
            new ApplicationStep(),
            controllerStep
        };

        foreach (var step in steps)
            step.Execute(config, entityName);

        if (!provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
        {
            var prev = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(config.SolutionPath);
                var infraProj = $"{config.SolutionName}.Infrastructure/{config.SolutionName}.Infrastructure.csproj";
                var startProj = $"{config.StartupProject}/{config.StartupProject}.csproj";
                var migName = $"Auto_{entityName}_{DateTime.UtcNow:yyyyMMddHHmmss}";
                Program.RunCommand($"dotnet ef migrations add {migName} --project {infraProj} --startup-project {startProj} --output-dir Migrations", config.SolutionPath);
                Program.RunCommand($"dotnet ef database update --project {infraProj} --startup-project {startProj}", config.SolutionPath);
            }
            finally
            {
                Directory.SetCurrentDirectory(prev);
            }
        }

        if (!config.Entities.TryGetValue(entityName, out var state))
            state = new EntityStatus();
        state.HasCrud = true;
        config.Entities[entityName] = state;
        ConfigManager.Save(config.SolutionPath, config);

        Console.WriteLine($"CRUD for {entityName} generated using {provider} provider.");
    }
}
