using System.IO;

namespace DotNetArch.Scaffolding.Steps;

public class EntityStep : IScaffoldStep
{
    public void Execute(string solution, string entity, string provider, string basePath)
    {
        var dir = Path.Combine(basePath, $"{solution}.Core", "Domain", entity);
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{entity}.cs");
        if (File.Exists(file)) return;
        var content = $$"""
namespace {{solution}}.Core.Domain.{{entity}};

public class {{entity}}
{
    public int Id { get; set; }
}
""";
        File.WriteAllText(file, content);
    }
}
