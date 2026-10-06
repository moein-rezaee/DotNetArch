using {{App}}.Application.Abstractions.Messaging;
using {{App}}.Application.Abstractions.Persistence;
using {{App}}.Infrastructure.Messaging;
using {{App}}.Infrastructure.Persistence;
using {{App}}.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace {{App}}.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers everything the Infrastructure layer owns: options, DbContext, repositories, unit of work, adapters.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .PostConfigure(options =>
            {
                // Secret: supplied by the environment (or .env), never by appsettings.
                var connectionString = configuration[DatabaseOptions.ConnectionStringKey];
                if (!string.IsNullOrWhiteSpace(connectionString))
                    options.ConnectionString = connectionString;
            })
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                $"Database connection string is missing. Set {DatabaseOptions.ConnectionStringKey} (environment or .env).")
            .ValidateOnStart();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var database = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            {{UseProviderStatement}}
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, MediatorDomainEventDispatcher>();
        return services;
    }

    /// <summary>Applies pending migrations when <c>Database:MigrateOnStartup</c> is true. Call once during startup.</summary>
    public static async Task InitializeInfrastructureAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        if (!options.MigrateOnStartup)
            return;

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
