using System.IO;
using DotNetArch.Scaffolding;

namespace DotNetArch.Scaffolding.Steps;

public class EntityStep : IScaffoldStep
{
    public void Execute(SolutionConfig config, string entity)
    {
        var solution = config.SolutionName;
        var basePath = config.SolutionPath;
        var plural = Naming.Pluralize(entity);
        var dir = Path.Combine(basePath, $"{solution}.Core", "Features", plural);
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{entity}.cs");
        if (File.Exists(file)) return;
        var content = """
namespace {{solution}}.Core.Features.{{entities}};

public class {{entity}}
{
    public int Id { get; set; }
}
""";
        File.WriteAllText(file, content
            .Replace("{{solution}}", solution)
            .Replace("{{entity}}", entity)
            .Replace("{{entities}}", plural));
    }
}
