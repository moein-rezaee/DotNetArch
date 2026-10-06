namespace DotNetArch.Cli.Commands;

internal sealed class DockerAddCommand : SolutionCommandBase
{
    public override bool Matches(string[] args) => CommandMatch.Is(args, "docker", "add");

    public override int Run(string[] args)
    {
        var parsed = CommandArgs.Parse(args, 2);
        var config = LoadConfig(parsed, out _);
        if (config == null)
            return 1;

        if (parsed.Get("docker-registry") is { Length: > 0 } registry)
            config.DockerRegistry = registry;
        if (parsed.Get("nuget-source") is { Length: > 0 } nugetSource)
            config.NuGetSource = nugetSource;
        if (parsed.Get("nuget-source-name") is { Length: > 0 } nugetName)
            config.NuGetSourceName = nugetName;

        OpsGenerator.AddNuGetConfig(config);
        OpsGenerator.AddDocker(config);
        return 0;
    }
}
