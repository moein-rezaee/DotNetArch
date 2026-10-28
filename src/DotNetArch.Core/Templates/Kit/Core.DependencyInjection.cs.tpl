using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Corevia.Kit.{{Name}};

public static class DependencyInjection
{
    /// <summary>Registers the {{Name}} core: the registry and selection of providers. Providers add themselves with their own <c>AddCorevia{{Name}}...</c> method.</summary>
    public static IServiceCollection AddCorevia{{Name}}(this IServiceCollection services, IConfiguration? configuration = null)
    {
        return services;
    }
}
