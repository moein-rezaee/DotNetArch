using {{App}}.Application;
using {{App}}.Cli;
using {{App}}.Cli.Configuration;
using {{App}}.Infrastructure;
using {{App}}.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder();

// Same configuration-load step as the Api and the MCP host: appsettings (non-sensitive) + .env + environment (secrets).
// The command-line arguments belong to the command, not to configuration.
var contentRoot = File.Exists(Path.Combine(builder.Environment.ContentRootPath, "appsettings.json"))
    ? builder.Environment.ContentRootPath
    : AppContext.BaseDirectory;
builder.Configuration.AddAppConfiguration(contentRoot, builder.Environment.EnvironmentName);

// Each layer registers its own dependencies; the CLI host adds only its commands. The CLI is a peer of the MCP host: both send the
// same MediatR requests as the API controllers, straight into the Application layer.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddCliHost();

using var host = builder.Build();
await host.Services.InitializeInfrastructureAsync();

using var scope = host.Services.CreateScope();
return await scope.ServiceProvider.GetRequiredService<CliApp>().RunAsync(args);
