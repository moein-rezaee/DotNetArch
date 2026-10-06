namespace DotNetArch.Cli.Commands;

internal sealed class NewSolutionCommand : ICommand
{
    public bool Matches(string[] args) => CommandMatch.Is(args, "new", "solution");

    public int Run(string[] args)
    {
        var parsed = CommandArgs.Parse(args, 2);
        var solutionName = parsed.Positionals.LastOrDefault();
        var outputPath = parsed.Get("output");
        var startup = parsed.Get("startup");
        var style = parsed.Get("style");
        var noDatabase = parsed.Has("no-database");
        var layout = parsed.Get("layout") ?? SolutionConfig.V2Layout;
        var provider = parsed.Get("database");
        var ops = new OpsOptions(
            Ci: parsed.Get("ci"),
            GitRemote: parsed.Get("git-remote"),
            GitHost: parsed.Get("git-host"),
            GitProvider: parsed.Get("git-provider"),
            DockerRegistry: parsed.Get("docker-registry"),
            NuGetSource: parsed.Get("nuget-source"),
            NuGetSourceName: parsed.Get("nuget-source-name"),
            NoDocker: parsed.Has("no-docker"),
            NoGit: parsed.Has("no-git"));

        if (string.IsNullOrWhiteSpace(solutionName))
            solutionName = ToolHost.Ask("Enter solution name");
        if (string.IsNullOrWhiteSpace(solutionName))
        {
            ToolHost.Error("Solution name is required.");
            return 1;
        }

        if (!Identifier.IsValidSolutionName(solutionName))
        {
            ToolHost.Error($"'{solutionName}' is not a valid solution name.", "Use letters, digits and underscores; dots may separate segments; start with a letter.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
            outputPath = ToolHost.Ask("Output path", Directory.GetCurrentDirectory());
        if (string.IsNullOrWhiteSpace(startup))
            startup = $"{solutionName}.API";
        if (string.IsNullOrWhiteSpace(style))
            style = ToolHost.AskOption("Select API style", new[] { "controller", "fast" }).ToLower();
        if (string.IsNullOrWhiteSpace(style))
            style = "controller";

        SolutionGenerator.Generate(new SolutionRequest(solutionName, outputPath!, startup!, style!, noDatabase ? "None" : provider, layout, ops));
        return 0;
    }
}
