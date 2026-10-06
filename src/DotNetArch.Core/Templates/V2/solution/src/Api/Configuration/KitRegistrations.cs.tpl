// <dotnet-arch:kit-usings> (kit namespaces are added above this line)

namespace {{App}}.Api.Configuration;

public static class KitRegistrations
{
    /// <summary>
    /// Registers every kit: each provider first, then the kit's single entry point (providers and Core are referenced only here, in the
    /// composition root). Maintained by <c>dotnet-arch add kit</c>.
    /// </summary>
    public static IServiceCollection AddKits(this IServiceCollection services, IConfiguration configuration)
    {
        // <dotnet-arch:kits> (kit registrations are added above this line)
        return services;
    }
}
