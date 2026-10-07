namespace DotNetArch.Cli.Commands;

internal sealed class NewCrudCommand : SolutionCommandBase
{
    public override bool Matches(string[] args) => CommandMatch.Is(args, "new", "crud");

    public override int Run(string[] args)
    {
        var parsed = CommandArgs.Parse(args, 2);
        var entity = parsed.Get("entity");
        if (string.IsNullOrWhiteSpace(entity))
            entity = ToolHost.Ask("Enter entity name");
        if (string.IsNullOrWhiteSpace(entity))
        {
            ToolHost.Error("Entity name is required.");
            return 1;
        }

        entity = Identifier.Sanitize(entity);
        var config = LoadConfig(parsed, out _);
        if (config == null)
            return 1;

        using var migrations = MigrationScope(parsed);
        CrudScaffolder.Generate(config, entity);
        return 0;
    }
}
