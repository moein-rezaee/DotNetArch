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

        SolutionGenerator.Generate(new SolutionRequest(solutionName, outputPath!, startup!, style!, noDatabase ? "None" : null));
        return 0;
    }
}
