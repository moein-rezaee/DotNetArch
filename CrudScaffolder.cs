using System;
using DotNetArch.Scaffolding;
using DotNetArch.Scaffolding.Steps;

static class CrudScaffolder
{
    public static void Generate(string solutionName, string entityName, string basePath)
    {
        if (string.IsNullOrWhiteSpace(solutionName) || string.IsNullOrWhiteSpace(entityName))
        {
            Console.WriteLine("Solution and entity names are required.");
            return;
        }

        var provider = DatabaseProviderSelector.Choose();
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
            step.Execute(solutionName, entityName, provider, basePath);

        Console.WriteLine($"CRUD for {entityName} generated using {provider} provider.");
    }
}
