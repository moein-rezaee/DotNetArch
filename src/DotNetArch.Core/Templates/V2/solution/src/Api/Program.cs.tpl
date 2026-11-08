// <dotnet-arch:kit-usings> (kit namespaces of the profile are added above this line)
using {{App}}.Api.Configuration;
using {{App}}.Application;
using {{App}}.Infrastructure.Configuration;
using {{App}}.Infrastructure;

// <dotnet-arch:kit-bootstrap> (profile lines that run before the host builder exists)
var builder = WebApplication.CreateBuilder(args);

// One configuration-load step: appsettings (non-sensitive) + .env file + environment (secrets / run-time values).
builder.Configuration.AddAppConfiguration(builder.Environment.ContentRootPath, builder.Environment.EnvironmentName, args);
// <dotnet-arch:kit-builder> (profile registrations that need the builder)

// Each layer registers its own dependencies; the composition root only calls them.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi(builder.Configuration)
    .AddKits(builder.Configuration);

var app = builder.Build();
app.UseApiLocalization(); // first: the request's language applies to everything after it, errors included
// <dotnet-arch:kit-app> (profile middleware, before the Api pipeline)

// `--export-openapi [file]` writes the OpenAPI document and exits: no server, no database (used when the service is registered).
if (OpenApiExport.TryGetPath(args, out var openApiPath))
{
    app.UseApi();
    await OpenApiExport.WriteAsync(app.Services, openApiPath);
    return;
}

await app.Services.InitializeInfrastructureAsync();
app.UseApi();

await app.RunAsync();

/// <summary>Makes the entry point visible to <c>WebApplicationFactory</c> in integration tests.</summary>
public partial class Program;
