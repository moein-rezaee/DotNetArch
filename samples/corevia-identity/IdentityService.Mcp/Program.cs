using System.Net;
using System.Text;
using Corevia.Kit.ConfigCenterExtension;
using Corevia.Kit.ConfigLoaderExtension;
using Corevia.Kit.ConfigLoaderExtension.Abstractions;
using Corevia.Kit.ConfigLoaderExtension.Core;
using Corevia.Kit.ErrorHandling;
using FluentValidation;
using IdentityService.Application;
using IdentityService.Application.Features.Identity.Options;
using IdentityService.Infrastructure;
using IdentityService.Mcp.Health;
using IdentityService.Mcp.Mcp;
using Corevia.Kit.JwtSecurityExtension;
using Corevia.Kit.Logging;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Corevia.Kit.SecretStoreExtension;
using Corevia.Kit.ServiceDiscoveryExtension;

var preliminaryConfiguration = new ConfigurationManager();
preliminaryConfiguration.SetBasePath(Directory.GetCurrentDirectory());
preliminaryConfiguration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
preliminaryConfiguration.AddEnvironmentVariables();
preliminaryConfiguration.AddCommandLine(args);

var transport = preliminaryConfiguration["MCP_TRANSPORT"]
                ?? preliminaryConfiguration["Mcp:Transport"]
                ?? "http";
var environmentName = preliminaryConfiguration["ASPNETCORE_ENVIRONMENT"]
                      ?? preliminaryConfiguration["DOTNET_ENVIRONMENT"]
                      ?? "Production";

// HTTP is the default transport; stdio is development-only (enforced by McpTransportSelector).
if (McpTransportSelector.Resolve(transport, environmentName) == McpTransportKind.Http)
{
    await RunHttpAsync(args);
}
else
{
    await RunStdioAsync(args);
}

static async Task RunStdioAsync(string[] args)
{
    // stdio is a process transport. Use the generic host so no Kestrel listener is
    // created and stdout remains exclusively reserved for MCP JSON-RPC frames.
    var builder = Host.CreateApplicationBuilder(args);
    ConfigureConfiguration(builder.Configuration, builder.Environment.ContentRootPath, args);
    ConfigureMcpLogging(builder);

    // The canonical bootstrap must run before options or dependent registration.
    BootstrapCentralConfiguration(builder);
    ConfigureCommonServices(builder);
    RegisterMcpServer(builder, isHttp: false);

    using var host = builder.Build();
    await host.RunAsync();
}

static async Task RunHttpAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    ConfigureConfiguration(builder.Configuration, builder.Environment.ContentRootPath, args);
    ConfigureMcpLogging(builder);

    // The canonical bootstrap must run before options or dependent registration.
    BootstrapCentralConfiguration(builder);
    ConfigureCommonServices(builder);
    RegisterMcpServer(builder, isHttp: true);
    builder.Services.AddMcpHealth();

    var bindAddress = ResolveValue(builder.Configuration, "MCP_HTTP_BIND_ADDRESS", "Mcp:HttpBindAddress") ?? "127.0.0.1";
    var port = ResolvePort(ResolveValue(builder.Configuration, "MCP_HTTP_PORT", "Mcp:HttpPort"));
    var httpsEnabled = ResolveBoolean(ResolveValue(builder.Configuration, "MCP_HTTP_HTTPS_ENABLED", "Mcp:HttpHttpsEnabled"));
    var privateNetworkTrusted = ResolveBoolean(ResolveValue(builder.Configuration, "MCP_PRIVATE_NETWORK_TRUSTED", "Mcp:PrivateNetworkTrusted"));
    ValidateNetworkProfile(bindAddress, httpsEnabled, privateNetworkTrusted);
    builder.WebHost.UseUrls(BuildUrl(bindAddress, port, httpsEnabled));

    var app = builder.Build();

    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/mcp") && !IsAllowedHost(context, builder.Configuration))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Invalid MCP Host header.");
            return;
        }

        if (context.Request.Path.StartsWithSegments("/mcp") && !IsAllowedOrigin(context, builder.Configuration))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Invalid MCP Origin header.");
            return;
        }

        await next();
    });

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapMcpHealth();
    app.MapMcp("/mcp").RequireAuthorization();

    await app.RunAsync();
}

static void ConfigureConfiguration(
    IConfigurationManager configuration,
    string contentRootPath,
    string[] args)
{
    configuration
        .SetBasePath(contentRootPath)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
}

static void ConfigureMcpLogging(IHostApplicationBuilder builder)
{
    // Kit correlation services are WebApplicationBuilder-only; the stdio generic host has no HTTP
    // pipeline to correlate, so only the HTTP transport registers them.
    if (builder is WebApplicationBuilder webBuilder)
    {
        webBuilder.AddLoggingExtension();
    }

    if (builder.Environment.IsProduction())
    {
        // Production noise filters carried over from the legacy shared/Logging bootstrap (Kit does not apply them).
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        builder.Logging.AddFilter("Ocelot", LogLevel.Warning);
    }

    // MCP stdio reserves stdout for JSON-RPC frames. Keep every application log on stderr.
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(logging => logging.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Logging.SetMinimumLevel(LogLevel.Information);
}

static void BootstrapCentralConfiguration(IHostApplicationBuilder builder)
{
    // Corevia.Kit.ConfigLoader copies the already-registered services into its bootstrap container, so the
    // Config (Consul) and Secrets (Vault) providers must be registered before the loader runs.
    builder.Services.AddConfigCenterExtension(builder.Configuration);
    builder.Services.AddSecretStoreExtension(builder.Configuration);

    if (builder is WebApplicationBuilder webBuilder)
    {
        webBuilder.AddConfigLoaderExtension();
        return;
    }

    // KIT GAP (tracked follow-up): Corevia.Kit.ConfigLoader only exposes AddConfigLoaderExtension for
    // WebApplicationBuilder, while the stdio transport uses the generic host (no Kestrel listener; stdout is
    // reserved for MCP frames). This composes the Kit's own public ConfigLoaderService/ConfigurationKeyMapper
    // exactly as AddConfigLoaderExtension does. Delete once the Kit adds an IHostApplicationBuilder overload.
    var bootstrapServices = new ServiceCollection();
    foreach (var descriptor in builder.Services)
    {
        ((ICollection<ServiceDescriptor>)bootstrapServices).Add(descriptor);
    }

    bootstrapServices.AddSingleton<IConfiguration>(builder.Configuration);
    bootstrapServices.AddSingleton<IConfigurationKeyMapper, ConfigurationKeyMapper>();
    bootstrapServices.AddSingleton<IConfigurationBootstrapService, ConfigLoaderService>();

#pragma warning disable ASP0000 // one-shot bootstrap container, identical to the Kit's own loader
    using var provider = bootstrapServices.BuildServiceProvider();
#pragma warning restore ASP0000
    var merged = provider.GetRequiredService<IConfigurationBootstrapService>()
        .LoadMergedConfiguration(builder.Configuration);
    if (merged.Count > 0)
    {
        builder.Configuration.AddInMemoryCollection(merged);
    }
}

static void ConfigureCommonServices(IHostApplicationBuilder builder)
{
    var configuration = builder.Configuration;

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AssemblyMarker).Assembly));
    builder.Services.AddValidatorsFromAssemblyContaining<AssemblyMarker>();
    builder.Services.AddErrorHandling();

    // Shared Corevia runtime providers are composition-root dependencies only. MCP code
    // below never calls them or reaches the database directly. Config and Secrets providers
    // are registered earlier by BootstrapCentralConfiguration.
    builder.Services.AddServiceDiscoveryExtension(configuration);

    builder.Services
        .AddOptions<JwtOptions>()
        .Bind(configuration.GetSection("Jwt"))
        .PostConfigure(options =>
        {
            var secret = configuration["JWT_SECRET"];
            if (!string.IsNullOrWhiteSpace(secret))
            {
                options.Secret = secret;
            }
        })
        .Validate(options => Encoding.UTF8.GetByteCount(options.Secret ?? string.Empty) >= 16,
            "JWT secret must be at least 16 bytes (set JWT_SECRET or the centralized secret).")
        .ValidateOnStart();

    builder.Services
        .AddOptions<IdentityMcpOptions>()
        .Bind(configuration.GetSection(IdentityMcpOptions.SectionName))
        .PostConfigure(options =>
        {
            options.Transport = ResolveValue(configuration, "MCP_TRANSPORT", "Mcp:Transport") ?? options.Transport;
            options.ServerName = ResolveValue(configuration, "MCP_SERVER_NAME", "Mcp:ServerName") ?? options.ServerName;
            options.ServerVersion = ResolveValue(configuration, "MCP_SERVER_VERSION", "Mcp:ServerVersion") ?? options.ServerVersion;
            options.Audience = ResolveValue(configuration, "MCP_AUDIENCE", "Mcp:Audience") ?? options.Audience;
            options.HttpBindAddress = ResolveValue(configuration, "MCP_HTTP_BIND_ADDRESS", "Mcp:HttpBindAddress") ?? options.HttpBindAddress;
            if (int.TryParse(ResolveValue(configuration, "MCP_HTTP_PORT", "Mcp:HttpPort"), out var port))
            {
                options.HttpPort = port;
            }

            if (bool.TryParse(ResolveValue(configuration, "MCP_HTTP_HTTPS_ENABLED", "Mcp:HttpHttpsEnabled"), out var https))
            {
                options.HttpHttpsEnabled = https;
            }

            if (bool.TryParse(ResolveValue(configuration, "MCP_PRIVATE_NETWORK_TRUSTED", "Mcp:PrivateNetworkTrusted"), out var privateNetwork))
            {
                options.PrivateNetworkTrusted = privateNetwork;
            }

            var delegationKey = configuration["MCP_DELEGATION_SIGNING_KEY"];
            if (!string.IsNullOrWhiteSpace(delegationKey))
            {
                options.DelegationSigningKey = delegationKey;
            }

            var approvalKey = configuration["MCP_APPROVAL_SIGNING_KEY"];
            if (!string.IsNullOrWhiteSpace(approvalKey))
            {
                options.ApprovalSigningKey = approvalKey;
            }
        })
        .ValidateOnStart();

    builder.Services.AddJwtSecurityExtension(configuration);
    builder.Services.AddAuthorization();
    builder.Services.AddIdentityInfrastructure(configuration);

    builder.Services.AddSingleton<IMcpPrincipalResolver, DefaultMcpPrincipalResolver>();
    builder.Services.AddSingleton<IMcpNonceStore, InMemoryMcpNonceStore>();
    builder.Services.AddSingleton<McpSignedEnvelopeVerifier>();
    builder.Services.AddScoped<McpExecutionContextResolver>();
    builder.Services.AddScoped<McpApprovalVerifier>();
    builder.Services.AddScoped<IMcpAuditSink, StructuredMcpAuditSink>();
    builder.Services.AddScoped<IdentityMcpToolExecutor>();
}

static void RegisterMcpServer(IHostApplicationBuilder builder, bool isHttp)
{
    var configuration = builder.Configuration;
    var mcpServer = builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = ResolveValue(configuration, "MCP_SERVER_NAME", "Mcp:ServerName") ?? "identity-service-mcp",
            Version = ResolveValue(configuration, "MCP_SERVER_VERSION", "Mcp:ServerVersion") ?? "0.1.0",
            Title = "Identity Service MCP",
            Description = "Corevia-standard Identity capabilities with server-enforced authorization and redaction."
        };
    })
    .WithTools<IdentityMcpTools>()
    .WithTools<IdentityMcpAdminTools>();

    if (isHttp)
    {
        mcpServer.WithHttpTransport(options =>
        {
            // Stateless Streamable HTTP avoids session affinity and is sufficient for the
            // request/response Identity tool surface. No server-to-client sampling is enabled.
            options.SessionMode = HttpServerSessionMode.Stateless;
        });
    }
    else
    {
        mcpServer.WithStdioServerTransport();
    }
}

static int ResolvePort(string? value)
    => int.TryParse(value, out var port) && port is >= 1 and <= 65535 ? port : 5271;

static string? ResolveValue(IConfiguration configuration, string environmentKey, string sectionKey)
    => configuration[environmentKey] ?? configuration[sectionKey];

static bool ResolveBoolean(string? value)
    => bool.TryParse(value, out var result) && result;

static string BuildUrl(string address, int port, bool https)
{
    var host = address.Contains(':') && !address.StartsWith("[", StringComparison.Ordinal)
        ? $"[{address}]"
        : address;
    return $"{(https ? "https" : "http")}://{host}:{port}";
}

static void ValidateNetworkProfile(string address, bool https, bool privateNetworkTrusted)
{
    if (!IPAddress.TryParse(address, out var ipAddress))
    {
        throw new InvalidOperationException("MCP_HTTP_BIND_ADDRESS must be a numeric IP address.");
    }

    if (IPAddress.IsLoopback(ipAddress))
    {
        return;
    }

    if (!https && !privateNetworkTrusted)
    {
        throw new InvalidOperationException("Non-loopback MCP HTTP binding requires MCP_PRIVATE_NETWORK_TRUSTED=true.");
    }

    if (!https)
    {
        // Private HTTP is allowed only when the operator explicitly asserts an
        // equivalent private-network boundary. Public/customer-server access must use HTTPS.
        return;
    }
}

static bool IsAllowedHost(HttpContext context, IConfiguration configuration)
{
    var host = context.Request.Host.Host;
    if (string.IsNullOrWhiteSpace(host))
    {
        return false;
    }

    var configured = ResolveList(configuration, "MCP_ALLOWED_HOSTS", "Mcp:AllowedHosts", ["127.0.0.1", "localhost", "::1"]);
    return configured.Any(value => string.Equals(value.Trim('[', ']'), host.Trim('[', ']'), StringComparison.OrdinalIgnoreCase));
}

static bool IsAllowedOrigin(HttpContext context, IConfiguration configuration)
{
    if (!context.Request.Headers.TryGetValue("Origin", out var originValues) || originValues.Count == 0)
    {
        return true;
    }

    var origin = originValues[0];
    var configured = ResolveList(configuration, "MCP_ALLOWED_ORIGINS", "Mcp:AllowedOrigins", []);
    return configured.Contains(origin, StringComparer.Ordinal);
}

static string[] ResolveList(
    IConfiguration configuration,
    string environmentKey,
    string sectionKey,
    string[] fallback)
{
    var csv = configuration[environmentKey];
    if (!string.IsNullOrWhiteSpace(csv))
    {
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    var values = configuration.GetSection(sectionKey)
        .GetChildren()
        .Select(item => item.Value)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!)
        .ToArray();
    return values.Length > 0 ? values : fallback;
}
