using System.Security.Claims;
using System.Text;
using Corevia.Kit.ConfigCenterExtension;
using Corevia.Kit.ConfigLoaderExtension;
using Corevia.Kit.ErrorHandling;
using FluentValidation;
using FluentValidation.AspNetCore;
using IdentityService.Application;
using IdentityService.Application.Features.Identity.Options;
using IdentityService.Application.Features.Identity.Services;
using IdentityService.Domain.Interfaces;
using IdentityService.Api.Health;
using IdentityService.Infrastructure;
using Corevia.Kit.Logging;
using Corevia.Kit.Logging.Providers.Console;
using MediatR;
using Microsoft.Extensions.Hosting;
using Corevia.Kit.SecretStoreExtension;
using Corevia.Kit.ServiceDiscoveryExtension;
using Corevia.Kit.Swagger;

using Corevia.Kit.JwtSecurityExtension;

RuntimeEnvironmentFileLoader.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

// Corevia.Kit.ConfigLoader copies the already-registered services into its bootstrap container, so the
// Config (Consul) and Secrets (Vault) providers must be registered before the loader runs.
builder.Services.AddConfigCenterExtension(builder.Configuration);
builder.Services.AddSecretStoreExtension(builder.Configuration);
builder.AddConfigLoaderExtension();

// Corevia.Kit.Logging console provider: correlation + SimpleConsole + optional COREVIA_LOG_DIR file logging.
builder.AddConsoleLoggingProvider();
if (builder.Environment.IsProduction())
{
    // Production noise filters carried over from the legacy shared/Logging bootstrap. The Kit logging
    // provider does not apply them (tracked Kit gap); they are plain logging configuration, not a duplicate.
    builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
    builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
    builder.Logging.AddFilter("Ocelot", LogLevel.Warning);
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerExtension(builder.Configuration, builder.Environment);

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AssemblyMarker).Assembly));
builder.Services.AddValidatorsFromAssemblyContaining<AssemblyMarker>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddErrorHandling();

// Centralized service discovery (Config and Secrets providers are registered before the loader above)
builder.Services.AddServiceDiscoveryExtension(builder.Configuration);

// Options (bind non-sensitive from appsettings; secrets from env via Configuration)
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .PostConfigure(options =>
    {
        var secret = builder.Configuration["JWT_SECRET"];
        if (!string.IsNullOrWhiteSpace(secret)) options.Secret = secret!;
    })
    .Validate(options =>
    {
        var len = Encoding.UTF8.GetByteCount(options.Secret ?? string.Empty);
        return len >= 16; // 128-bit minimum for HS256
    }, "JWT secret must be at least 16 bytes (set JWT_SECRET env).")
    .ValidateOnStart();

builder.Services
    .AddOptions<RootAdminOptions>()
    .Configure(options =>
    {
        options.PhoneNumber = builder.Configuration["IDENTITY_ROOT_PHONE"];
    });

builder.Services
    .AddOptions<IdentityClientOptions>()
    .Bind(builder.Configuration.GetSection("IdentityClient"))
    .PostConfigure(options =>
    {
        // Override DefaultPublicClientId from env if provided
        var envClientId = builder.Configuration["IDENTITY_DEFAULT_PUBLIC_CLIENT_ID"];
        if (!string.IsNullOrWhiteSpace(envClientId))
        {
            options.DefaultPublicClientId = envClientId;
        }

        // Override M2M client secrets from env (format: M2M_<CLIENT_ID_UPPER>_SECRET)
        if (options.M2MClients is not null)
        {
            foreach (var (clientId, config) in options.M2MClients)
            {
                var envKey = string.IsNullOrWhiteSpace(config.RequiredSecretKey)
                    ? $"M2M_{clientId.ToUpperInvariant().Replace("-", "_")}_SECRET"
                    : config.RequiredSecretKey;
                var secret = builder.Configuration[envKey];
                if (!string.IsNullOrWhiteSpace(secret))
                {
                    config.Secret = secret;
                }
            }
        }
    });

builder.Services.AddJwtSecurityExtension(builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdmin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
            ctx.User.Claims.Any(c =>
                (c.Type == "role" || c.Type == ClaimTypes.Role) &&
                string.Equals(c.Value, "SuperAdmin", StringComparison.Ordinal)));
    });

    // Service-level access: IdentityService
    options.AddPolicy("IdentityServiceAccess", policy =>
        policy.RequireAssertion(ctx =>
        {
            if (IsSuperAdmin(ctx.User))
            {
                return true;
            }

            return ctx.User.FindAll("scope")
                .Any(c => string.Equals(c.Value, "identity.write", StringComparison.Ordinal));
        }));

    options.AddPolicy("IdentitySelf", policy =>
        policy.RequireAssertion(ctx =>
        {
            if (IsSuperAdmin(ctx.User))
            {
                return true;
            }

            return ctx.User.FindAll("scope")
                .Any(c => string.Equals(c.Value, "identity.self", StringComparison.Ordinal));
        }));
});

static bool IsSuperAdmin(ClaimsPrincipal user)
{
    return user.Claims.Any(c =>
        (c.Type == "role" || c.Type == ClaimTypes.Role) &&
        string.Equals(c.Value, "SuperAdmin", StringComparison.Ordinal));
}

builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddIdentityHealth();

// JWT service
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IIdentitySeedService, IdentitySeedService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbInitializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
        await dbInitializer.InitializeAsync();
        Console.WriteLine("✅ Database initialization completed successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database initialization failed: {ex.Message}");
        Console.WriteLine($"Exception Type: {ex.GetType().Name}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        throw;
    }
}

app.UseSwaggerExtension(builder.Environment);
app.UseLoggingExtension();
app.UseErrorHandling();
var urls = builder.Configuration["ASPNETCORE_URLS"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
var hasHttps = urls?.Contains("https", StringComparison.OrdinalIgnoreCase) == true;
if (hasHttps)
{
    app.UseHttpsRedirection();
}
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Root info endpoint (plain text)
app.MapGet("/", (HttpContext ctx) =>
{
    var config = ctx.RequestServices.GetRequiredService<IConfiguration>();
    var version = config["IDENTITY_APP_VERSION"] ?? "0.0.0";
    var req = ctx.Request;
    var scheme = req.Scheme;
    var host = req.Host.HasValue ? req.Host.Value : "localhost";
    var port = req.Host.Port?.ToString() ?? (scheme == "https" ? "443" : "80");
    var swaggerUrl = $"{scheme}://{host}/swagger/index.html";
    var sb = new StringBuilder()
        .AppendLine("Identity Service is running")
        .AppendLine($"- Version: {version}")
        .AppendLine($"- Environment: {ctx.RequestServices.GetRequiredService<IHostEnvironment>().EnvironmentName}")
        .AppendLine($"- Port: {port}")
        .AppendLine($"- Swagger: {swaggerUrl}")
        .AppendLine("- Base API: /v1/api")
        .AppendLine($"- Time: {DateTimeOffset.UtcNow:u}");
    return Results.Text(sb.ToString(), "text/plain");
}).ExcludeFromDescription();

// Liveness (/health, unchanged payload) and readiness (/health/ready, database check, 503 when unavailable)
app.MapGet("/health", HealthEndpoints.Liveness)
   .ExcludeFromDescription();
app.MapIdentityReadiness();

app.Run();
