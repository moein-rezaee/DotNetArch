namespace DotNetArch.Core.Scaffolding.Kits;

/// <summary>A third-party package a kit part needs. The version is looked up per target-framework major.</summary>
public sealed record KitPackage(string Id, Func<int, string> Version)
{
    public static KitPackage Fixed(string id, string version) => new(id, _ => version);
}

/// <summary>One provider of a built-in kit: its packages, the configuration it reads and the secrets it needs.</summary>
public sealed record KitProviderRecipe(
    string Name,
    string Description,
    IReadOnlyList<KitPackage> Packages,
    IReadOnlyDictionary<string, string> Settings,
    IReadOnlyDictionary<string, string> SettingDocs,
    IReadOnlyDictionary<string, string> Secrets);

/// <summary>A built-in business area (MediaStorage, Cache, MessageBroker): contract, shared core and its providers.</summary>
public sealed record KitRecipe(
    string Area,
    string Description,
    string DescriptionFa,
    string ContractSummary,
    IReadOnlyList<KitPackage> CorePackages,
    IReadOnlyList<KitProviderRecipe> Providers)
{
    public KitProviderRecipe? FindProvider(string name) =>
        Providers.FirstOrDefault(provider => provider.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
