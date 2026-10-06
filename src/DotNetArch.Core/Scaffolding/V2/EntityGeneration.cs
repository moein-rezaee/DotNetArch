using DotNetArch.Core.Templating;

namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>Entity lookups and the small artefacts (enums, constants, events) shared across v2 commands.</summary>
internal static class EntityGeneration
{
    public static bool EntityExists(SolutionConfig config, string entity) =>
        File.Exists(Path.Combine(config.SolutionPath, V2Names.For(config, entity).DomainProject, "Entities", $"{entity}.cs"));

    // --- enums and constants ---------------------------------------------------------------------------------------
    public static bool GenerateEnum(SolutionConfig config, string? entity, string name) =>
        GenerateArtifact(config, entity, name, "Enums", "Enum", fileName: name);

    public static bool GenerateConstant(SolutionConfig config, string? entity, string name) =>
        GenerateArtifact(config, entity, name, "Constants", "Constants", fileName: name);

    private static bool GenerateArtifact(SolutionConfig config, string? entity, string name, string folder, string template, string fileName)
    {
        Identifier.RequireIdentifier(name, $"{folder.TrimEnd('s').ToLowerInvariant()} name");
        Identifier.RequireIfPresent(entity, "entity name");

        string relativeFolder;
        string @namespace;
        var domain = $"src/{config.SolutionName}.Domain";
        if (string.IsNullOrWhiteSpace(entity))
        {
            relativeFolder = $"{domain}/{folder}";
            @namespace = $"{config.SolutionName}.Domain.{folder}";
        }
        else
        {
            if (!EntityExists(config, entity))
            {
                ToolHost.Error($"Entity '{entity}' does not exist.");
                return false;
            }

            var plural = Naming.Pluralize(entity);
            relativeFolder = $"{domain}/{folder}/{plural}";
            @namespace = $"{config.SolutionName}.Domain.{folder}.{plural}";
        }

        var tokens = new Dictionary<string, string> { ["Namespace"] = @namespace, ["Name"] = name };
        var writer = new FileWriter(config.SolutionPath);
        if (!writer.Write($"{relativeFolder}/{fileName}.cs", TemplateRenderer.RenderTemplate($"V2/artifacts/{template}.cs.tpl", tokens)))
        {
            ToolHost.Error($"{name} already exists.");
            return false;
        }

        return true;
    }

    // --- events ----------------------------------------------------------------------------------------------------
    public static string[] ListEvents(SolutionConfig config, string entity)
    {
        var names = V2Names.For(config, entity);
        var folder = Path.Combine(config.SolutionPath, names.DomainProject, "Events", names.Plural);
        if (!Directory.Exists(folder))
            return Array.Empty<string>();

        return Directory.GetFiles(folder, $"{entity}*Event.cs")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Select(file => file[entity.Length..^"Event".Length])
            .Where(name => name.Length > 0)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool GenerateEvent(SolutionConfig config, string entity, string eventName)
    {
        Identifier.RequireIdentifier(entity, "entity name");
        Identifier.RequireIdentifier(eventName, "event name");
        eventName = char.ToUpperInvariant(eventName[0]) + eventName[1..];

        if (!EntityExists(config, entity))
        {
            ToolHost.Error($"Entity '{entity}' does not exist.");
            return false;
        }

        var names = V2Names.For(config, entity);
        var tokens = names.Tokens();
        tokens["EventName"] = eventName;
        var writer = new FileWriter(config.SolutionPath);
        if (!writer.Write($"{names.DomainProject}/Events/{names.Plural}/{entity}{eventName}Event.cs",
                TemplateRenderer.RenderTemplate("V2/event/DomainEvent.cs.tpl", tokens)))
        {
            ToolHost.Error($"Event '{eventName}' already exists for entity '{entity}'.");
            return false;
        }

        writer.Write($"{names.DomainProject}/Entities/{entity}.{eventName}Event.cs", TemplateRenderer.RenderTemplate("V2/event/EntityPartial.cs.tpl", tokens));
        return true;
    }

    public static bool AddSubscriber(SolutionConfig config, string eventEntity, string eventName, string subscriberEntity)
    {
        Identifier.RequireIdentifier(eventEntity, "entity name");
        Identifier.RequireIdentifier(eventName, "event name");
        Identifier.RequireIdentifier(subscriberEntity, "subscriber entity name");

        if (!EntityExists(config, subscriberEntity))
        {
            ToolHost.Error($"Subscriber entity '{subscriberEntity}' does not exist.");
            return false;
        }

        if (!ListEvents(config, eventEntity).Contains(eventName))
        {
            ToolHost.Error($"Event '{eventName}' for entity '{eventEntity}' does not exist.");
            return false;
        }

        var subscriber = V2Names.For(config, subscriberEntity);
        var tokens = subscriber.Tokens();
        tokens["EventEntity"] = eventEntity;
        tokens["EventName"] = eventName;
        tokens["EventPlural"] = Naming.Pluralize(eventEntity);

        var writer = new FileWriter(config.SolutionPath);
        if (!writer.Write($"{subscriber.FeatureFolder}/Events/On{eventEntity}{eventName}Handler.cs",
                TemplateRenderer.RenderTemplate("V2/event/Subscriber.cs.tpl", tokens)))
        {
            ToolHost.Error($"Subscriber '{subscriberEntity}' already exists for {eventEntity}{eventName}Event.");
            return false;
        }

        return true;
    }
}
