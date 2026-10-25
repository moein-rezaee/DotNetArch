using {{App}}.Application.Features.{{Plural}}.Commands.Create{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Commands.Delete{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Commands.Update{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Queries.Get{{Entity}}ById;
using {{App}}.Application.Features.{{Plural}}.Queries.Get{{Plural}};
using MediatR;

namespace {{App}}.Cli.Commands.{{Plural}};

/// <summary>List {{PluralLower}}, one page at a time (the same use case as the API and the MCP tool {{ToolName}}_list).</summary>
internal sealed class {{Entity}}ListCliCommand : CliCommand
{
    public override string Group => "{{CliGroup}}";

    public override string Name => "list";

    public override string Description => "List {{PluralLower}}, one page at a time.";

    public override IReadOnlyList<CliOption> Options { get; } =
    [
        new("page", "int", false, "1-based page number."),
        new("size", "int", false, "Items per page, 1-100."),
    ];

    public override Task<object?> RunAsync(CliContext context, ISender sender, CancellationToken cancellationToken) =>
        Box(sender.Send(new Get{{Plural}}Query(context.Optional<int?>("page") ?? 1, context.Optional<int?>("size") ?? 20), cancellationToken));
}

internal sealed class {{Entity}}GetCliCommand : CliCommand
{
    public override string Group => "{{CliGroup}}";

    public override string Name => "get";

    public override string Description => "Get one {{EntityLower}} by id.";

    public override IReadOnlyList<CliOption> Options { get; } = [new("id", "guid", true, "The {{EntityLower}} id.")];

    public override Task<object?> RunAsync(CliContext context, ISender sender, CancellationToken cancellationToken) =>
        Box(sender.Send(new Get{{Entity}}ByIdQuery(context.Require<Guid>("id")), cancellationToken));
}

internal sealed class {{Entity}}CreateCliCommand : CliCommand
{
    public override string Group => "{{CliGroup}}";

    public override string Name => "create";

    public override string Description => "Create a {{EntityLower}}.";

    public override IReadOnlyList<CliOption> Options { get; } = [new("name", "string", true, "The {{EntityLower}} name.")];

    public override Task<object?> RunAsync(CliContext context, ISender sender, CancellationToken cancellationToken) =>
        Box(sender.Send(new Create{{Entity}}Command(context.Require<string>("name")), cancellationToken));
}

internal sealed class {{Entity}}UpdateCliCommand : CliCommand
{
    public override string Group => "{{CliGroup}}";

    public override string Name => "update";

    public override string Description => "Rename a {{EntityLower}}.";

    public override IReadOnlyList<CliOption> Options { get; } =
    [
        new("id", "guid", true, "The {{EntityLower}} id."),
        new("name", "string", true, "The new name."),
    ];

    public override Task<object?> RunAsync(CliContext context, ISender sender, CancellationToken cancellationToken) =>
        Box(sender.Send(new Update{{Entity}}Command(context.Require<Guid>("id"), context.Require<string>("name")), cancellationToken));
}

internal sealed class {{Entity}}DeleteCliCommand : CliCommand
{
    public override string Group => "{{CliGroup}}";

    public override string Name => "delete";

    public override string Description => "Delete a {{EntityLower}}. This cannot be undone.";

    public override IReadOnlyList<CliOption> Options { get; } = [new("id", "guid", true, "The {{EntityLower}} id.")];

    public override async Task<object?> RunAsync(CliContext context, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new Delete{{Entity}}Command(context.Require<Guid>("id")), cancellationToken);
        return "Deleted.";
    }
}
