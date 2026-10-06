namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>
/// Pinned NuGet versions written into generated <c>Directory.Packages.props</c>. This is the single place to bump versions;
/// generated solutions never float. Keyed by the .NET major (8 or 9).
/// </summary>
public static class PackageCatalog
{
    public const string MediatR = "12.5.0"; // last Apache-2.0 line (D-04)
    public const string FluentValidation = "12.1.1";
    public const string Swashbuckle = "6.9.0";

    public static string SdkVersion(int major) => $"{major}.0.100";

    public static string EfCore(int major) => major >= 9 ? "9.0.20" : "8.0.31";

    public static string AspNet(int major) => major >= 9 ? "9.0.20" : "8.0.31";

    public static string ExtDependencyInjection(int major) => major >= 9 ? "9.0.20" : "8.0.2";

    public static string ExtOptionsConfiguration(int major) => major >= 9 ? "9.0.20" : "8.0.0";

    public static string ExtHostingAbstractions(int major) => major >= 9 ? "9.0.20" : "8.0.1";

    public static string EfProviderVersion(string provider, int major) => provider switch
    {
        DatabaseProviders.Postgres => major >= 9 ? "9.0.4" : "8.0.11",
        _ => EfCore(major)
    };
}
