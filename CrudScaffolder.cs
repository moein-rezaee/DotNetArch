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
            Program.Error("Solution and entity names are required.");
            return;
        }

        if (config.Entities.TryGetValue(entityName, out var existing) && existing.HasCrud)
        {
            Program.Info($"CRUD for {entityName} already exists; ensuring missing parts are added.");
            // Continue to run steps to add any missing files/methods.
        }

        var provider = config.DatabaseProvider;
        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = DatabaseProviderSelector.Choose();
            config.DatabaseProvider = provider;
            ConfigManager.Save(config.SolutionPath, config);
        }

        if (!provider.Equals("Mongo", StringComparison.OrdinalIgnoreCase) && !provider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            if (!Program.EnsureEfTool(config.SolutionPath))
            {
                Program.Error("dotnet-ef installation failed; CRUD generation canceled.");
                return;
            }
        }
        var controllerStep = config.ApiStyle.Equals("fast", StringComparison.OrdinalIgnoreCase)
            ? (IScaffoldStep)new MinimalApiStep()
            : new ControllerStep();

        var stepsList = new System.Collections.Generic.List<IScaffoldStep>
        {
            new ProjectUpdateStep(),
        };
        // Always generate minimal entity (even in no-db) so app code compiles
        stepsList.Add(new EntityStep());
        if (!provider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            stepsList.Add(new DbContextStep());
            stepsList.Add(new RepositoryStep());
            stepsList.Add(new UnitOfWorkStep());
        }
        stepsList.Add(new ApplicationStep());
        stepsList.Add(controllerStep);
        var steps = stepsList.ToArray();

        foreach (var step in steps)
            step.Execute(config, entityName);

        if (!provider.Equals("Mongo", StringComparison.OrdinalIgnoreCase) && !provider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            var prev = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(config.SolutionPath);
                if (Program.RunCommand("dotnet build", config.SolutionPath))
                {
                    var infraProj = $"{config.SolutionName}.Infrastructure/{config.SolutionName}.Infrastructure.csproj";
                    var startProj = $"{config.StartupProject}/{config.StartupProject}.csproj";
                    var migName = $"Auto_{entityName}_{DateTime.UtcNow:yyyyMMddHHmmss}";
                    if (Program.RunCommand($"dotnet ef migrations add {migName} --project {infraProj} --startup-project {startProj} --output-dir {PathConstants.MigrationsRelativePath}", config.SolutionPath))
                    {
                        Program.RunCommand($"dotnet ef database update --project {infraProj} --startup-project {startProj}", config.SolutionPath);
                    }
                }
                else
                {
                    Program.Error("Build failed; skipping migrations.");
                }
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

        Program.Success($"CRUD for {entityName} generated using {provider} provider.");
    }
}
