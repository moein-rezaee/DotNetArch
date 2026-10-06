using System.Text.Json;
using IdentityService.Infrastructure.Database.Context;
using IdentityService.Infrastructure.Database.Initialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityService.Api.Health;

/// <summary>
/// Liveness handler (<c>/health</c>, cheap and unchanged; the route itself is mapped in Program.cs) and readiness
/// (<c>/health/ready</c>, checks the database and answers 503 when it is unavailable). Built on the ASP.NET Core
/// health-check APIs; the Kit has no health capability.
/// </summary>
public static class HealthEndpoints
{
    public const string ReadyTag = "ready";

    public static IResult Liveness() => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow });

    public static IServiceCollection AddIdentityHealth(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, new[] { ReadyTag });
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityReadiness(this IEndpointRouteBuilder app)
    {
        // Readiness: dependencies are usable. Unhealthy => 503 (default ResultStatusCodes).
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = WriteAsync
        }).ExcludeFromDescription();

        return app;
    }

    private static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            timestamp = DateTimeOffset.UtcNow,
            checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString().ToLowerInvariant())
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}

/// <summary>Database readiness through the EF Core can-connect check with a short timeout.</summary>
public sealed class DatabaseHealthCheck(IdentityDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthContext, CancellationToken cancellationToken = default)
        => await context.CanConnectAsync(cancellationToken: cancellationToken).ConfigureAwait(false)
            ? HealthCheckResult.Healthy("Database reachable.")
            : HealthCheckResult.Unhealthy("Database unreachable.");
}
