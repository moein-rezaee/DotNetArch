using System;
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

        Console.WriteLine($"CRUD for {entityName} generated using {provider} provider.");
    }
}
