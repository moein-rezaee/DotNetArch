using System.IO;
using System.Linq;
using DotNetArch.Scaffolding;

namespace DotNetArch.Scaffolding.Steps;

public class UnitOfWorkStep : IScaffoldStep
{
    public void Execute(string solution, string entity, string provider, string basePath, string startupProject)
    {
        var plural = Naming.Pluralize(entity);
        var coreDir = Path.Combine(basePath, $"{solution}.Core", "Interfaces");
        Directory.CreateDirectory(coreDir);
        var uowInterfaceFile = Path.Combine(coreDir, "IUnitOfWork.cs");
        if (!File.Exists(uowInterfaceFile))
        {
            var iContent = """
using System;
using System.Threading.Tasks;
using {{solution}}.Core.Domain.{{entities}};

namespace {{solution}}.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    I{{entity}}Repository {{entity}}Repository { get; }
    Task<int> SaveChangesAsync();
}
""";
            File.WriteAllText(uowInterfaceFile, iContent
                .Replace("{{solution}}", solution)
                .Replace("{{entity}}", entity)
                .Replace("{{entities}}", plural));
        }
        else
        {
            var lines = File.ReadAllLines(uowInterfaceFile).ToList();
            var usingLine = $"using {solution}.Core.Domain.{plural};";
            if (!lines.Contains(usingLine))
                lines.Insert(0, usingLine);
            var propLine = $"    I{entity}Repository {entity}Repository {{ get; }}";
            if (!lines.Any(l => l.Contains(propLine)))
            {
                var saveIdx = lines.FindIndex(l => l.Contains("SaveChangesAsync"));
                lines.Insert(saveIdx, propLine);
            }
            File.WriteAllLines(uowInterfaceFile, lines);
        }

        var infraDir = Path.Combine(basePath, $"{solution}.Infrastructure");
        Directory.CreateDirectory(infraDir);
        var uowFile = Path.Combine(infraDir, "UnitOfWork.cs");
        var lower = char.ToLowerInvariant(entity[0]) + entity.Substring(1);
        if (!File.Exists(uowFile))
        {
            var uContent = """
using System.Threading.Tasks;
using {{solution}}.Core.Domain.{{entities}};
using {{solution}}.Core.Interfaces;
using {{solution}}.Infrastructure.Persistence;
using {{solution}}.Infrastructure.{{entities}};

namespace {{solution}}.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private I{{entity}}Repository? _{{lower}}Repository;

    public UnitOfWork(AppDbContext context) => _context = context;

    public I{{entity}}Repository {{entity}}Repository => _{{lower}}Repository ??= new {{entity}}Repository(_context);

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

    public void Dispose() => _context.Dispose();
}
""";
            File.WriteAllText(uowFile, uContent
                .Replace("{{solution}}", solution)
                .Replace("{{entity}}", entity)
                .Replace("{{entities}}", plural)
                .Replace("{{lower}}", lower));
        }
        else
        {
            var lines = File.ReadAllLines(uowFile).ToList();
            var usingDomain = $"using {solution}.Core.Domain.{plural};";
            var usingRepo = $"using {solution}.Infrastructure.{plural};";
            if (!lines.Contains(usingRepo)) lines.Insert(0, usingRepo);
            if (!lines.Contains(usingDomain)) lines.Insert(0, usingDomain);

            var fieldLine = $"    private I{entity}Repository? _{lower}Repository;";
            if (!lines.Any(l => l.Contains(fieldLine)))
            {
                var contextIndex = lines.FindIndex(l => l.Contains("AppDbContext _context"));
                lines.Insert(contextIndex + 1, fieldLine);
            }

            var propLine = $"    public I{entity}Repository {entity}Repository => _{lower}Repository ??= new {entity}Repository(_context);";
            if (!lines.Any(l => l.Contains($"public I{entity}Repository {entity}Repository")))
            {
                var saveIdx = lines.FindIndex(l => l.Contains("SaveChangesAsync"));
                lines.Insert(saveIdx, propLine);
            }

            File.WriteAllLines(uowFile, lines);
        }
    }
}
