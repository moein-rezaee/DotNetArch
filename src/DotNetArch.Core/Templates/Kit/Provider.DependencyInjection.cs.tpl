using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Corevia.Kit.{{Name}}.{{Segment}}.{{Provider}};

public static class DependencyInjection
{
    /// <summary>Plugs the {{Provider}} provider into the {{Name}} kit (the host adds this package and calls this method; nothing else changes).</summary>
    public static IServiceCollection AddCorevia{{Name}}{{ProviderId}}(this IServiceCollection services, IConfiguration? configuration = null)
    {
        return services;
    }
}
