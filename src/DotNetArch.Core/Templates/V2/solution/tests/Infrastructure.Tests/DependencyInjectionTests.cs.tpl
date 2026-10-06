using {{App}}.Application;
using {{App}}.Application.Abstractions.Persistence;
using {{App}}.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace {{App}}.Infrastructure.Tests;

public class DependencyInjectionTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public void Resolves_the_unit_of_work_when_the_connection_string_comes_from_the_environment_key()
    {
        using var provider = Build(new() { [DatabaseOptions.ConnectionStringKey] = "Data Source=:memory:" });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.Equal("Data Source=:memory:", scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString);
    }

    [Fact]
    public void A_missing_connection_string_fails_options_validation_with_the_key_name()
    {
        using var provider = Build(new());

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<DatabaseOptions>>().Value);

        Assert.Contains(DatabaseOptions.ConnectionStringKey, exception.Message);
    }

    [Fact]
    public void Connection_strings_in_appsettings_sections_are_overridden_by_the_secret_key()
    {
        using var provider = Build(new()
        {
            ["Database:ConnectionString"] = "from-appsettings",
            [DatabaseOptions.ConnectionStringKey] = "from-environment"
        });

        Assert.Equal("from-environment", provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString);
    }
}
