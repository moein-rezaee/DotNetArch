using {{App}}.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
// <dotnet-arch:service-usings> (service namespaces are added above this line)

namespace {{App}}.Application;

public static class DependencyInjection
{
    /// <summary>Registers everything the Application layer owns: handlers, validators, pipeline behaviours.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(AssemblyMarker).Assembly);
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(AssemblyMarker).Assembly, includeInternalTypes: true);
        services.TryAddSingleton(TimeProvider.System);
        // <dotnet-arch:services> (business services are registered above this line)
        return services;
    }
}
