using System.IO;
using System.Linq;

namespace DotNetArch.Scaffolding.Steps;

public class DbContextStep : IScaffoldStep
{
    public void Execute(string solution, string entity, string provider)
    {
        var dir = Path.Combine($"{solution}.Infrastructure", "Persistence");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "AppDbContext.cs");
        if (!File.Exists(file))
        {
            var content = $$"""
using Microsoft.EntityFrameworkCore;
using {{solution}}.Core.Domain.{{entity}};

namespace {{solution}}.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<{{entity}}> {{entity}}s { get; set; }
}
""";
            File.WriteAllText(file, content);
        }
        else
        {
            var lines = File.ReadAllLines(file).ToList();
            var usingLine = $"using {solution}.Core.Domain.{entity};";
            if (!lines.Contains(usingLine))
                lines.Insert(0, usingLine);

            var propLine = $"    public DbSet<{entity}> {entity}s {{ get; set; }}";
            if (!lines.Any(l => l.Contains($"DbSet<{entity}>") ))
            {
                var insertIndex = lines.Count - 2;
                lines.Insert(insertIndex, propLine);
            }
            File.WriteAllLines(file, lines);
        }
    }
}
