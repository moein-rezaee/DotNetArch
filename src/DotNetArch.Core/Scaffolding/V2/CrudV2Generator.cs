using DotNetArch.Core.Templating;

namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>Generates the vertical slice for one entity in a v2 solution: domain entity, CQRS use cases, persistence mapping and controller.</summary>
internal static class CrudV2Generator
{
    public static void Generate(SolutionConfig config, string entity)
    {
        Identifier.RequireIdentifier(entity, "entity name");
        var names = V2Names.For(config, entity);
        var tokens = names.Tokens();
        var writer = new FileWriter(config.SolutionPath);
        var feature = names.FeatureFolder;

        if (config.Entities.TryGetValue(entity, out var existing) && existing.HasCrud)
            ToolHost.Info($"CRUD for {entity} already exists; ensuring missing parts are added.");

        (string Template, string Output)[] files =
        {
            ("DomainEntity", $"{names.DomainProject}/Entities/{entity}.cs"),
            ("EfConfiguration", $"{names.InfrastructureProject}/Persistence/Configurations/{entity}Configuration.cs"),
            ("Dtos", $"{feature}/Dtos/{entity}Dtos.cs"),
            ("CreateCommand", $"{feature}/Commands/Create{entity}/Create{entity}Command.cs"),
            ("CreateHandler", $"{feature}/Commands/Create{entity}/Create{entity}CommandHandler.cs"),
            ("CreateValidator", $"{feature}/Commands/Create{entity}/Create{entity}CommandValidator.cs"),
            ("UpdateCommand", $"{feature}/Commands/Update{entity}/Update{entity}Command.cs"),
            ("UpdateHandler", $"{feature}/Commands/Update{entity}/Update{entity}CommandHandler.cs"),
            ("UpdateValidator", $"{feature}/Commands/Update{entity}/Update{entity}CommandValidator.cs"),
            ("DeleteCommand", $"{feature}/Commands/Delete{entity}/Delete{entity}Command.cs"),
            ("DeleteHandler", $"{feature}/Commands/Delete{entity}/Delete{entity}CommandHandler.cs"),
            ("GetByIdQuery", $"{feature}/Queries/Get{entity}ById/Get{entity}ByIdQuery.cs"),
            ("GetByIdHandler", $"{feature}/Queries/Get{entity}ById/Get{entity}ByIdQueryHandler.cs"),
            ("GetPagedQuery", $"{feature}/Queries/Get{names.Plural}/Get{names.Plural}Query.cs"),
            ("GetPagedHandler", $"{feature}/Queries/Get{names.Plural}/Get{names.Plural}QueryHandler.cs"),
            ("GetPagedValidator", $"{feature}/Queries/Get{names.Plural}/Get{names.Plural}QueryValidator.cs"),
            config.ApiStyle.Equals("fast", StringComparison.OrdinalIgnoreCase)
                ? ("Endpoints", $"{names.ApiProject}/Endpoints/{names.Plural}/{names.Plural}Endpoints.cs")
                : ("Controller", $"{names.ApiProject}/Controllers/{names.Plural}/{names.Plural}Controller.cs"),
        };

        foreach (var (template, output) in files)
            writer.Write(output, TemplateRenderer.RenderTemplate($"V2/crud/{template}.cs.tpl", tokens));

        // Tests ride along with the slice whenever the solution has test projects (generated unless --no-tests).
        if (Directory.Exists(Path.Combine(config.SolutionPath, "tests", $"{config.SolutionName}.Domain.Tests")))
        {
            var tests = new List<(string Template, string Output)>
            {
                ("DomainTests", $"tests/{config.SolutionName}.Domain.Tests/Entities/{entity}Tests.cs"),
                ("ApplicationTests", $"tests/{config.SolutionName}.Application.Tests/Features/{names.Plural}/{entity}HandlerTests.cs"),
                ("InfrastructureTests", $"tests/{config.SolutionName}.Infrastructure.Tests/Persistence/{entity}PersistenceTests.cs"),
            };

            // API tests drive a real database file: only meaningful (and runnable anywhere) with SQLite.
            if (config.DatabaseProvider.Equals(DatabaseProviders.Sqlite, StringComparison.OrdinalIgnoreCase))
                tests.Add(("ApiTests", $"tests/{config.SolutionName}.Api.Tests/Features/{names.Plural}ApiTests.cs"));

            foreach (var (template, output) in tests)
                writer.Write(output, TemplateRenderer.RenderTemplate($"V2/crud-tests/{template}.cs.tpl", tokens));
        }

        if (writer.Created.Count == 0)
            ToolHost.Info($"Nothing to add for {entity}: all files already exist.");
        else
            ToolHost.Success($"Generated {writer.Created.Count} files for {entity} ({writer.Skipped.Count} already existed).");

        if (config.McpEnabled)
            McpV2Generator.AddEntityTools(config, entity);

        MigrationService.AddMigration(config, $"Auto_{entity}");

        if (!config.Entities.TryGetValue(entity, out var state))
            state = new EntityStatus();
        state.HasCrud = true;
        config.Entities[entity] = state;
        ConfigManager.Save(config.SolutionPath, config);

        ToolHost.Success($"CRUD for {entity} generated using {config.DatabaseProvider} provider.");
    }
}
