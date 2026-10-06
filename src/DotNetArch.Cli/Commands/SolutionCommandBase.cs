namespace DotNetArch.Cli.Commands;

/// <summary>Shared lookup for commands that operate on an existing solution (<c>--output</c>, saved path, or cwd).</summary>
internal abstract class SolutionCommandBase : ICommand
{
    public abstract bool Matches(string[] args);

    public abstract int Run(string[] args);

    protected static SolutionConfig? LoadConfig(CommandArgs args, out string basePath)
    {
        var outputPath = args.Get("output");
        if (string.IsNullOrWhiteSpace(outputPath))
            outputPath = PathState.Load() ?? Directory.GetCurrentDirectory();

        basePath = outputPath!;
        var config = ConfigManager.Load(basePath);
        if (config == null)
            ToolHost.Error("Solution configuration not found. Run 'new solution' first.");
        return config;
    }

    /// <summary>Asks for an entity until it exists; blank means "common" when <paramref name="allowBlank"/> is set.</summary>
    protected static string? ResolveEntity(string? entity, string prompt, Func<string, bool> exists, bool allowBlank, out bool cancelled)
    {
        cancelled = false;
        while (true)
        {
            if (string.IsNullOrWhiteSpace(entity))
                entity = ToolHost.Ask(prompt);
            if (string.IsNullOrWhiteSpace(entity))
            {
                if (allowBlank)
                    return null;
                ToolHost.Error("Entity name is required.");
                cancelled = true;
                return null;
            }

            entity = Identifier.Sanitize(entity);
            if (!exists(entity))
            {
                ToolHost.Error($"Entity '{entity}' does not exist.");
                entity = null;
                continue;
            }
            return entity;
        }
    }
}
