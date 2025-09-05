using System.IO;
using System.Linq;
using DotNetArch.Scaffolding;

namespace DotNetArch.Scaffolding.Steps;

public class DbContextStep : IScaffoldStep
{
    public void Execute(SolutionConfig config, string entity)
    {
        var solution = config.SolutionName;
        var basePath = config.SolutionPath;
        var plural = Naming.Pluralize(entity);
        var dir = Path.Combine(basePath, $"{solution}.Infrastructure", "Persistence");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "AppDbContext.cs");
        if (!File.Exists(file))
        {
            var content = """
using Microsoft.EntityFrameworkCore;
using {{solution}}.Core.Features.{{entities}}.Entities;

namespace {{solution}}.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<{{entity}}> {{entities}} { get; set; }
}
""";
            File.WriteAllText(file, content
                .Replace("{{solution}}", solution)
                .Replace("{{entity}}", entity)
                .Replace("{{entities}}", plural));
        }
        else
        {
            var lines = File.ReadAllLines(file).ToList();
            var usingLine = $"using {solution}.Core.Features.{plural}.Entities;";
            if (!lines.Contains(usingLine))
                lines.Insert(0, usingLine);

            var propLine = $"    public DbSet<{entity}> {plural} {{ get; set; }}";
            if (!lines.Any(l => l.Contains($"DbSet<{entity}>") ))
            {
                var insertIndex = lines.Count - 2;
                lines.Insert(insertIndex, propLine);
            }
            File.WriteAllLines(file, lines);
        }
    }
}

