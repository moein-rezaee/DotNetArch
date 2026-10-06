using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace {{App}}.Api.Tests.Support;

/// <summary>Hosts the real API in-process. The database is a throw-away file so every factory instance starts empty.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseFile = Path.Combine(Path.GetTempPath(), $"{{App}}-api-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("DATABASE_CONNECTION_STRING", {{TestConnectionExpression}});
    }

    /// <summary>Creates the schema from the model (no migrations needed) for tests that touch the database.</summary>
    public void EnsureDatabase()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<{{App}}.Infrastructure.Persistence.AppDbContext>();
        context.Database.EnsureCreated();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_databaseFile))
            File.Delete(_databaseFile);
    }
}
