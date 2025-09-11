using System.IO;
using DotNetArch.Scaffolding;

namespace DotNetArch.Scaffolding.Steps;

public class EntityStep : IScaffoldStep
{
    public void Execute(SolutionConfig config, string entity)
    {
        // Even in no-database mode, generate a minimal Entity (Id only)
        var solution = config.SolutionName;
        var basePath = config.SolutionPath;
        var plural = Naming.Pluralize(entity);
        var dir = Path.Combine(basePath, $"{solution}.Core", "Features", plural, "Entities");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{entity}.cs");
        if (File.Exists(file)) return;
        var content = """
namespace {{solution}}.Core.Features.{{entities}}.Entities;

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
