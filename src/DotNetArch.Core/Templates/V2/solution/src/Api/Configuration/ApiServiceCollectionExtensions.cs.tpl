using {{App}}.Api.Middleware;

namespace {{App}}.Api.Configuration;

public static class ApiServiceCollectionExtensions
{
    private const string CorsPolicy = "Default";

    /// <summary>Registers everything the Api layer owns: controllers, OpenAPI, problem details, CORS, health checks.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddHealthChecks();

        // CORS is opt-in: nothing is allowed until origins are listed in configuration (Cors:AllowedOrigins).
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        if (origins.Length > 0)
        {
            services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
        }

        return services;
    }

    public static WebApplication UseApi(this WebApplication app)
    {
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        if (app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 })
            app.UseCors(CorsPolicy);

        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health");
        return app;
    }
}
