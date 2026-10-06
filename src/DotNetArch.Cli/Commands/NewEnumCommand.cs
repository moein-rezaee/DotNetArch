namespace DotNetArch.Cli.Commands;

internal sealed class NewEnumCommand : SolutionCommandBase
{
    public override bool Matches(string[] args) => CommandMatch.Is(args, "new", "enum");

    public override int Run(string[] args)
    {
        var parsed = CommandArgs.Parse(args, 2);
        var config = LoadConfig(parsed, out _);
        if (config == null)
            return 1;

        var entity = ResolveEntity(parsed.Get("entity"), "Enter entity name (leave blank for common)",
            name => EnumScaffolder.EntityExists(config, name), allowBlank: true, out _);

        var enumName = parsed.Get("enum");
        if (string.IsNullOrWhiteSpace(enumName))
            enumName = ToolHost.Ask("Enter enum name");
        if (string.IsNullOrWhiteSpace(enumName))
        {
            ToolHost.Error("Enum name is required.");
            return 1;
        }

        enumName = Identifier.Sanitize(enumName);
        if (EnumScaffolder.Generate(config, entity, enumName))
        {
            ToolHost.Success(string.IsNullOrWhiteSpace(entity)
                ? $"Enum {enumName} generated under Common."
                : $"Enum {enumName} for {entity} generated.");
        }
        return 0;
    }
}
