using System.IO;
using System.Linq;
using DotNetArch.Scaffolding;

namespace DotNetArch.Scaffolding.Steps;

public class UnitOfWorkStep : IScaffoldStep
{
    public void Execute(SolutionConfig config, string entity)
    {
        var solution = config.SolutionName;
        var basePath = config.SolutionPath;
        var plural = Naming.Pluralize(entity);
        var appInterfaces = Path.Combine(basePath, $"{solution}.Application", "Common", "Interfaces");
        Directory.CreateDirectory(appInterfaces);
        var uowInterfaceFile = Path.Combine(appInterfaces, "IUnitOfWork.cs");
        if (!File.Exists(uowInterfaceFile))
        {
            var iContent = """
using System;
using System.Threading.Tasks;
using {{solution}}.Application.Common.Interfaces.Repositories;

namespace {{solution}}.Application.Common.Interfaces;

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
            var usingLine = $"using {solution}.Application.Common.Interfaces.Repositories;";
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
using {{solution}}.Application.Common.Interfaces;
using {{solution}}.Application.Common.Interfaces.Repositories;
using {{solution}}.Infrastructure.Persistence;
using {{solution}}.Infrastructure.Repositories;

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
            var usingRepo = $"using {solution}.Infrastructure.Repositories;";
            if (!lines.Contains(usingRepo)) lines.Insert(0, usingRepo);
            var usingInterface = $"using {solution}.Application.Common.Interfaces.Repositories;";
            if (!lines.Contains(usingInterface)) lines.Insert(0, usingInterface);

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
