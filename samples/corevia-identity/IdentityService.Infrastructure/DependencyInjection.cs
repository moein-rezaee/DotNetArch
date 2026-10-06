using Corevia.Kit.HttpClientRestExtension;
using Corevia.Kit.HttpClientRestExtension.Abstractions;
using Corevia.Kit.HttpClientRestExtension.Providers.Named;
using IdentityService.Domain.Interfaces;
using IdentityService.Infrastructure.Database;
using IdentityService.Infrastructure.Otp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Corevia.Kit.ServiceDiscoveryExtension.Abstractions;

namespace IdentityService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database provider adapter. Provider selection, connection-string building, and EF Core
        // UseNpgsql/UseSqlServer wiring are now delegated to Corevia.Kit.DatabaseConnection via
        // AddIdentityDatabase (IdentityService.Infrastructure.Database project) - IdentityService
        // itself never branches on the engine. Only the selected provider's DbContext is ever
        // constructed, so no SQL Server connection is attempted when the resolved value is
        // Postgres (and vice versa).
        services.AddIdentityDatabase(configuration);

        // OTP REST client: Corevia.Kit.Http named provider over a configured HttpClient
        // (base address resolved via service discovery, timeout and Accept header unchanged).
        services.AddHttpClientRestExtension(configuration);
        services.AddHttpClient(OtpRestClient.ProviderName, (sp, client) =>
        {
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("IdentityService.Otp.Discovery");
            var baseUrl = ResolveOtpBaseUrl(sp, configuration, logger);
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            }
            client.Timeout = TimeSpan.FromSeconds(ResolveOtpTimeoutSeconds(configuration));
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });
        services.AddSingleton<IHttpRestClientProvider>(sp =>
            new NamedHttpRestClientProvider(
                OtpRestClient.ProviderName,
                OtpRestClient.ProviderName,
                sp.GetRequiredService<IHttpClientFactory>(),
                defaultHeaders: null,
                sp.GetRequiredService<ILoggerFactory>()));
        services.AddScoped<IOtpClient, OtpRestClient>();

        return services;
    }

    private static string ResolveOtpBaseUrl(IServiceProvider serviceProvider, IConfiguration configuration, ILogger? logger)
    {
        var fallbackBaseUrl = configuration["OTP_BASE_URL"] ?? "http://otp-service:5254";
        var serviceNames = GetDiscoveryServiceNames(configuration, "ServiceDiscovery:Services:Otp");
        var scheme = configuration["ServiceDiscovery:Services:Otp:Scheme"] ?? "http";

        try
        {
            var discovery = serviceProvider.GetService<IServiceDiscoveryService>();
            if (discovery is not null && serviceNames.Count > 0)
            {
                foreach (var serviceName in serviceNames)
                {
                    var endpoint = discovery.ResolveOneAsync(serviceName).GetAwaiter().GetResult();
                    if (endpoint is not null && !string.IsNullOrWhiteSpace(endpoint.Address) && endpoint.Port > 0)
                    {
                        return $"{scheme}://{endpoint.Address}:{endpoint.Port}";
                    }
                }

                logger?.LogWarning("OTP discovery returned no endpoint for configured service names '{ServiceNames}'. Falling back to OTP_BASE_URL.", string.Join(", ", serviceNames));
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to resolve OTP endpoint from discovery. Falling back to OTP_BASE_URL.");
        }

        return fallbackBaseUrl;
    }

    private static int ResolveOtpTimeoutSeconds(IConfiguration configuration)
    {
        var raw = configuration["OTP_HTTP_TIMEOUT_SECONDS"] ?? configuration["IdentityOtp:TimeoutSeconds"];
        if (int.TryParse(raw, out var seconds) && seconds is >= 1 and <= 30)
        {
            return seconds;
        }

        return 8;
    }

    private static IReadOnlyList<string> GetDiscoveryServiceNames(IConfiguration configuration, string prefix)
    {
        var names = new List<string>();

        AppendNames(names, configuration[$"{prefix}:ServiceNames"]);
        AppendSingleName(names, configuration[$"{prefix}:ServiceName"]);

        // Backward-compatible fallback.
        AppendSingleName(names, "otp-service");

        return names;
    }

    private static void AppendNames(List<string> names, string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return;
        }

        foreach (var value in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            AppendSingleName(names, value);
        }
    }

    private static void AppendSingleName(List<string> names, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!names.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            names.Add(value);
        }
    }
}
