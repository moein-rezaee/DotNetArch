namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>Project names, folders and template tokens shared by every v2 generator.</summary>
internal sealed record V2Names(string App, string Entity)
{
    public string Plural { get; } = Naming.Pluralize(Entity);

    public string DomainProject => $"src/{App}.Domain";

    public string ApplicationProject => $"src/{App}.Application";

    public string InfrastructureProject => $"src/{App}.Infrastructure";

    public string ApiProject => $"src/{App}.Api";

    public string FeatureFolder => $"{ApplicationProject}/Features/{Plural}";

    public Dictionary<string, string> Tokens() => new()
    {
        ["App"] = App,
        ["Entity"] = Entity,
        ["Plural"] = Plural,
        ["RouteName"] = Naming.ToKebabCase(Plural),
    };

    public static V2Names For(SolutionConfig config, string entity) => new(config.SolutionName, entity);
}
