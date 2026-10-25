using {{App}}.Application;
using {{App}}.Application.Abstractions.Persistence;
using {{App}}.Cli.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace {{App}}.Cli.Tests.Support;

/// <summary>Runs command lines through the real MediatR pipeline with an in-memory unit of work: no database, no console.</summary>
internal sealed class CliHarness
{
    private readonly ServiceProvider _provider;

    public CliHarness()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton<IUnitOfWork>(new FakeUnitOfWork());
        services.AddCliCommands();
        _provider = services.BuildServiceProvider();
    }

    public IReadOnlyList<CliCommand> Commands => _provider.GetServices<CliCommand>().ToList();

    public async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var app = new CliApp(Commands, _provider.GetRequiredService<MediatR.ISender>(), output, error);
        var exitCode = await app.RunAsync(args);
        return (exitCode, output.ToString(), error.ToString());
    }
}
