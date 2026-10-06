using {{App}}.Api.Configuration;
using {{App}}.Application;
using {{App}}.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// One configuration-load step: appsettings (non-sensitive) + .env file + environment (secrets / run-time values).
builder.AddAppConfiguration(args);

// Each layer registers its own dependencies; the composition root only calls them.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi(builder.Configuration);

var app = builder.Build();

await app.Services.InitializeInfrastructureAsync();
app.UseApi();

await app.RunAsync();

/// <summary>Makes the entry point visible to <c>WebApplicationFactory</c> in integration tests.</summary>
public partial class Program;
