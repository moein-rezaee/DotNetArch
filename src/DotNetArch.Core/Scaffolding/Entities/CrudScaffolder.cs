using System;
using System.IO;
using DotNetArch.Core.Scaffolding;
using DotNetArch.Core.Scaffolding.Steps;

namespace DotNetArch.Core.Scaffolding.Entities;

public static class CrudScaffolder
{
    public static void Generate(SolutionConfig config, string entityName)
    {
        if (string.IsNullOrWhiteSpace(config.SolutionName) || string.IsNullOrWhiteSpace(entityName))
        {
            ToolHost.Error("Solution and entity names are required.");
            return;
        }

        if (config.Entities.TryGetValue(entityName, out var existing) && existing.HasCrud)
        {
            ToolHost.Info($"CRUD for {entityName} already exists; ensuring missing parts are added.");
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
            if (!SolutionTooling.EnsureEfTool(config.SolutionPath))
            {
                ToolHost.Error("dotnet-ef installation failed; CRUD generation canceled.");
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
            MigrationService.BuildAddAndApply(config, entityName);

        if (!config.Entities.TryGetValue(entityName, out var state))
            state = new EntityStatus();
        state.HasCrud = true;
        config.Entities[entityName] = state;
        ConfigManager.Save(config.SolutionPath, config);

        ToolHost.Success($"CRUD for {entityName} generated using {provider} provider.");
    }
}
