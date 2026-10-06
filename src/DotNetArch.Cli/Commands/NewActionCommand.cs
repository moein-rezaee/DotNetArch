namespace DotNetArch.Cli.Commands;

internal sealed class NewActionCommand : SolutionCommandBase
{
    private static readonly string[] AllowedMethods = { "GET", "POST", "PUT", "DELETE", "PATCH" };

    private static readonly Dictionary<string, string[]> ValidForMethod = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GET"] = new[] { "GETBYID", "GETALL", "GETLIST" },
        ["POST"] = new[] { "CREATE" },
        ["PUT"] = new[] { "UPDATE" },
        ["PATCH"] = new[] { "PATCH", "UPDATE" },
        ["DELETE"] = new[] { "DELETE" },
    };

    private static readonly HashSet<string> CrudNames =
        new(new[] { "CREATE", "UPDATE", "DELETE", "GETBYID", "GETALL", "GETLIST", "PATCH" }, StringComparer.OrdinalIgnoreCase);

    public override bool Matches(string[] args) => CommandMatch.Is(args, "new", "action");

    public override int Run(string[] args)
    {
        var parsed = CommandArgs.Parse(args, 2);
        var entity = parsed.Get("entity");
        var action = parsed.Get("action");
        var method = parsed.Get("method");

        if (string.IsNullOrWhiteSpace(entity))
            entity = ToolHost.Ask("Enter entity name");
        if (string.IsNullOrWhiteSpace(method))
            method = ToolHost.Ask("Enter HTTP method");
        if (string.IsNullOrWhiteSpace(entity) || string.IsNullOrWhiteSpace(method))
        {
            ToolHost.Error("Entity and method are required.");
            return 1;
        }

        if (!AllowedMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            ToolHost.Error($"Invalid HTTP method. Allowed methods: {string.Join(", ", AllowedMethods)}.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(action))
            action = ToolHost.Ask("Enter action name (leave empty to infer from method)");

        var autoAction = string.IsNullOrWhiteSpace(action);
        if (autoAction)
        {
            action = method.ToUpperInvariant() switch
            {
                "GET" => "GetById",
                "POST" => "Create",
                "PUT" => "Update",
                "DELETE" => "Delete",
                "PATCH" => "Patch",
                _ => "",
            };
        }

        // Validate explicit action name vs HTTP method (only for exact CRUD keywords)
        var upperMethod = method.ToUpperInvariant();
        var upperAction = (action ?? string.Empty).ToUpperInvariant();
        if (!autoAction && CrudNames.Contains(upperAction)
            && ValidForMethod.TryGetValue(upperMethod, out var allowed)
            && !allowed.Contains(upperAction, StringComparer.OrdinalIgnoreCase))
        {
            ToolHost.Error($"Action name '{action}' conflicts with HTTP method '{method}'. Allowed for {method}: {string.Join(", ", allowed)}.");
            return 1;
        }

        entity = Identifier.Sanitize(entity);
        action = Identifier.Sanitize(action);

        var config = LoadConfig(parsed, out _);
        if (config == null)
            return 1;

        ActionScaffolder.Generate(config, entity, action, upperMethod, autoAction);
        return 0;
    }
}
