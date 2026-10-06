using System.Text.Json;
using IdentityService.Infrastructure.Database.Context;
using IdentityService.Infrastructure.Database.Initialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityService.Mcp.Health;

/// <summary>
/// HTTP host health: <c>/health</c> liveness (unchanged payload) and <c>/health/ready</c> readiness that checks the
/// database (EF Core can-connect with a short timeout) and answers 503 when unavailable. Both are anonymous.
/// </summary>
public static class McpHealthEndpoints
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddMcpHealth(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<McpDatabaseHealthCheck>("database", HealthStatus.Unhealthy, new[] { ReadyTag });
        return services;
    }

    public static IEndpointRouteBuilder MapMcpHealth(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "identity-service-mcp" }))
            .AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = (context, report) =>
            {
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString().ToLowerInvariant(),
                    service = "identity-service-mcp",
                    checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString().ToLowerInvariant())
                }));
            }
        }).AllowAnonymous();
        return app;
    }
}

public sealed class McpDatabaseHealthCheck(IdentityDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthContext, CancellationToken cancellationToken = default)
        => await context.CanConnectAsync(cancellationToken: cancellationToken).ConfigureAwait(false)
            ? HealthCheckResult.Healthy("Database reachable.")
            : HealthCheckResult.Unhealthy("Database unreachable.");
}
