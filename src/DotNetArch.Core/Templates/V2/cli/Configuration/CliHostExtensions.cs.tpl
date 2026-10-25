using Microsoft.Extensions.DependencyInjection;

namespace {{App}}.Cli.Configuration;

public static class CliHostExtensions
{
    /// <summary>Registers the commands of this assembly. Tests use this and bring their own writers.</summary>
    public static IServiceCollection AddCliCommands(this IServiceCollection services)
    {
        foreach (var type in typeof(CliHostExtensions).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(CliCommand).IsAssignableFrom(t)))
            services.AddTransient(typeof(CliCommand), type);

        return services;
    }

    /// <summary>Registers the commands and the application that runs them on the console.</summary>
    public static IServiceCollection AddCliHost(this IServiceCollection services)
    {
        services.AddCliCommands();
        services.AddScoped(provider => new CliApp(
            provider.GetServices<CliCommand>(),
            provider.GetRequiredService<MediatR.ISender>(),
            Console.Out,
            Console.Error));
        return services;
    }
}
