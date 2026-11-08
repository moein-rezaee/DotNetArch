using System.Globalization;
using {{App}}.Api.Endpoints;
using {{App}}.Api.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;

namespace {{App}}.Api.Configuration;

public static class ApiServiceCollectionExtensions
{
    private const string CorsPolicy = "Default";

    /// <summary>Registers everything the Api layer owns: controllers, OpenAPI, problem details, CORS, health checks, localization.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
        services.AddEndpoints();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddHealthChecks();
        // Multi-culture by default: messages in Resources/SharedMessages*.resx, cultures from configuration (Localization:*).
        services.AddLocalization(options => options.ResourcesPath = "Resources");

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
        app.MapEndpoints();
        app.MapHealthChecks("/health").AllowAnonymous();
        // Liveness answers while the process runs; readiness runs every registered check (database and the others).
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready").AllowAnonymous();
        return app;
    }

    /// <summary>
    /// Sets the request's culture first, so every later step (error responses of the exception handler or of the ErrorHandling Kit,
    /// validation messages, localized texts) answers in the language the caller asked for.
    /// </summary>
    public static WebApplication UseApiLocalization(this WebApplication app)
    {
        app.UseRequestLocalization(LocalizationOptions(app.Configuration));
        return app;
    }

    /// <summary>The cultures a request may ask for (Accept-Language, <c>culture</c> query value or cookie); anything else falls back to the default.</summary>
    private static RequestLocalizationOptions LocalizationOptions(IConfiguration configuration)
    {
        var supported = configuration.GetSection("Localization:SupportedCultures").Get<string[]>() is { Length: > 0 } list ? list : ["en", "fa"];
        var fallback = configuration["Localization:DefaultCulture"] is { Length: > 0 } culture ? culture : supported[0];
        var cultures = supported.Select(name => new CultureInfo(name)).ToList();
        return new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(fallback),
            SupportedCultures = cultures,
            SupportedUICultures = cultures,
            ApplyCurrentCultureToResponseHeaders = true,
        };
    }
}
