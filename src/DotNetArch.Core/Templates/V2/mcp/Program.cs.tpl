using {{App}}.Application;
using {{App}}.Infrastructure;
using {{App}}.Infrastructure.Configuration;
using {{App}}.Mcp.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Same configuration-load step as the Api: appsettings (non-sensitive) + .env + environment (secrets).
builder.Configuration.AddAppConfiguration(builder.Environment.ContentRootPath, builder.Environment.EnvironmentName, args);

// Each layer registers its own dependencies; the MCP host adds only what is specific to MCP.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddMcpHost(builder.Configuration);

var app = builder.Build();

await app.Services.InitializeInfrastructureAsync();
app.UseMcpHost();

await app.RunAsync();

/// <summary>Makes the entry point visible to <c>WebApplicationFactory</c> in integration tests.</summary>
public partial class Program;
