namespace DotNetArch.Cli.Commands;

/// <summary>Fallback when no sub-command is given: walks the user through creating a solution.</summary>
internal sealed class InteractiveSolutionCommand : ICommand
{
    public bool Matches(string[] args) => true;

    public int Run(string[] args)
    {
        ToolHost.Info("Welcome to ScaffoldCleanArch Tool!");
        var solutionName = ToolHost.Ask("Enter the name of your solution");
        if (string.IsNullOrWhiteSpace(solutionName))
        {
            ToolHost.Error("Solution name cannot be empty!");
            return 1;
        }

        if (!Identifier.IsValidSolutionName(solutionName))
        {
            ToolHost.Error($"'{solutionName}' is not a valid solution name.", "Use letters, digits and underscores; dots may separate segments; start with a letter.");
            return 1;
        }

        var outputPath = ToolHost.Ask("Enter output path", Directory.GetCurrentDirectory());
        var style = ToolHost.AskOption("Select API style", new[] { "controller", "fast" }).ToLower();
        if (string.IsNullOrWhiteSpace(style)) style = "controller";

        SolutionGenerator.Generate(new SolutionRequest(solutionName, outputPath, $"{solutionName}.API", style));
        return 0;
    }
}
