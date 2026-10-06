namespace DotNetArch.Cli.Commands;

internal sealed class NewConstantCommand : SolutionCommandBase
{
    public override bool Matches(string[] args) => CommandMatch.Is(args, "new", "constant");

    public override int Run(string[] args)
    {
        var parsed = CommandArgs.Parse(args, 2);
        var config = LoadConfig(parsed, out _);
        if (config == null)
            return 1;

        var entity = ResolveEntity(parsed.Get("entity"), "Enter entity name (leave blank for common)",
            name => ConstantScaffolder.EntityExists(config, name), allowBlank: true, out _);

        var constantName = parsed.Get("constant");
        if (string.IsNullOrWhiteSpace(constantName))
            constantName = ToolHost.Ask("Enter constant name");
        if (string.IsNullOrWhiteSpace(constantName))
        {
            ToolHost.Error("Constant name is required.");
            return 1;
        }

        constantName = Identifier.Sanitize(constantName);
        if (ConstantScaffolder.Generate(config, entity, constantName))
        {
            ToolHost.Success(string.IsNullOrWhiteSpace(entity)
                ? $"Constant {constantName} generated under Common."
                : $"Constant {constantName} for {entity} generated.");
        }
        return 0;
    }
}
