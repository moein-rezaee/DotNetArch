using System;
using System.IO;
using DotNetArch.Scaffolding;

namespace DotNetArch.Scaffolding.Steps;

public class RepositoryStep : IScaffoldStep
{
    public void Execute(SolutionConfig config, string entity)
    {
        var solution = config.SolutionName;
        var basePath = config.SolutionPath;
        var plural = Naming.Pluralize(entity);

        // ensure core models directory and PagedResult
        var coreModelsDir = Path.Combine(basePath, $"{solution}.Core", "Common", "Models");
        Directory.CreateDirectory(coreModelsDir);
        var pagedResultFile = Path.Combine(coreModelsDir, "PagedResult.cs");
        if (!File.Exists(pagedResultFile))
        {
        var pagedContent = """
using System.Collections.Generic;

namespace {{solution}}.Core.Common.Models;

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);
""";
            File.WriteAllText(pagedResultFile, pagedContent.Replace("{{solution}}", solution));
        }

        // repository interface in Application layer
        var appCommon = Path.Combine(basePath, $"{solution}.Application", "Common");
        var ifaceDir = Path.Combine(appCommon, "Interfaces", "Repositories");
        Directory.CreateDirectory(ifaceDir);
        var ifaceFile = Path.Combine(ifaceDir, $"I{entity}Repository.cs");
        var ifaceTemplate = """
using System.Threading.Tasks;
using System.Collections.Generic;
using {{solution}}.Core.Common.Models;
using {{solution}}.Core.Features.{{entities}};

namespace {{solution}}.Application.Common.Interfaces.Repositories;

public interface I{{entity}}Repository
{
    Task<{{entity}}?> GetByIdAsync(int id);
    Task<List<{{entity}}>> GetAllAsync();
    Task<PagedResult<{{entity}}>> ListAsync(int page = 1, int pageSize = 10);
    Task AddAsync({{entity}} entity);
    Task UpdateAsync({{entity}} entity);
    Task DeleteAsync({{entity}} entity);
}
""";
        var ifaceText = ifaceTemplate
            .Replace("{{solution}}", solution)
            .Replace("{{entity}}", entity)
            .Replace("{{entities}}", plural);
        if (!File.Exists(ifaceFile))
        {
            File.WriteAllText(ifaceFile, ifaceText);
        }
        else
        {
            var text = File.ReadAllText(ifaceFile);
            if (!text.Contains("GetAllAsync"))
            {
                var insert = $@"    Task<{entity}?> GetByIdAsync(int id);{Environment.NewLine}    Task<List<{entity}>> GetAllAsync();{Environment.NewLine}    Task<PagedResult<{entity}>> ListAsync(int page = 1, int pageSize = 10);{Environment.NewLine}    Task AddAsync({entity} entity);{Environment.NewLine}    Task UpdateAsync({entity} entity);{Environment.NewLine}    Task DeleteAsync({entity} entity);{Environment.NewLine}";
                var idx = text.LastIndexOf("}");
                text = text.Insert(idx, insert);
            }
            if (!text.Contains("using " + solution + ".Core.Common.Models;"))
                text = "using " + solution + ".Core.Common.Models;" + Environment.NewLine + text;
            if (!text.Contains("using " + solution + ".Core.Features." + plural + ";"))
                text = "using " + solution + ".Core.Features." + plural + ";" + Environment.NewLine + text;
            File.WriteAllText(ifaceFile, text);
        }

        // repository implementation in Infrastructure
        var infraDir = Path.Combine(basePath, $"{solution}.Infrastructure", "Persistence", "Repositories");
        Directory.CreateDirectory(infraDir);
        var repoFile = Path.Combine(infraDir, $"{entity}Repository.cs");
        var repoTemplate = """
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using {{solution}}.Core.Common.Models;
using {{solution}}.Application.Common.Interfaces.Repositories;
using {{solution}}.Core.Features.{{entities}};
using {{solution}}.Infrastructure.Persistence;

namespace {{solution}}.Infrastructure.Persistence.Repositories;

public class {{entity}}Repository : I{{entity}}Repository
{
    private readonly AppDbContext _context;

    public {{entity}}Repository(AppDbContext context) => _context = context;

    public async Task AddAsync({{entity}} entity) => await _context.Set<{{entity}}>().AddAsync(entity);

    public async Task DeleteAsync({{entity}} entity)
    {
        _context.Set<{{entity}}>().Remove(entity);
        await Task.CompletedTask;
    }

    public async Task<{{entity}}?> GetByIdAsync(int id) => await _context.Set<{{entity}}>().FindAsync(id);

    public async Task<List<{{entity}}>> GetAllAsync() => await _context.Set<{{entity}}>().ToListAsync();

    public async Task<PagedResult<{{entity}}>> ListAsync(int page = 1, int pageSize = 10)
    {
        var query = _context.Set<{{entity}}>();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var total = await query.CountAsync();
        return new PagedResult<{{entity}}>(items, total, page, pageSize);
    }

    public Task UpdateAsync({{entity}} entity)
    {
        _context.Set<{{entity}}>().Update(entity);
        return Task.CompletedTask;
    }
}
""";
        var repoText = repoTemplate
            .Replace("{{solution}}", solution)
            .Replace("{{entity}}", entity)
            .Replace("{{entities}}", plural);
        if (!File.Exists(repoFile))
        {
            File.WriteAllText(repoFile, repoText);
        }
        else
        {
            var text = File.ReadAllText(repoFile);
            if (!text.Contains("GetAllAsync"))
            {
                var methods = $@"    public async Task AddAsync({entity} entity) => await _context.Set<{entity}>().AddAsync(entity);{Environment.NewLine}{Environment.NewLine}    public async Task DeleteAsync({entity} entity){Environment.NewLine}    {{{Environment.NewLine}        _context.Set<{entity}>().Remove(entity);{Environment.NewLine}        await Task.CompletedTask;{Environment.NewLine}    }}{Environment.NewLine}{Environment.NewLine}    public async Task<{entity}?> GetByIdAsync(int id) => await _context.Set<{entity}>().FindAsync(id);{Environment.NewLine}{Environment.NewLine}    public async Task<List<{entity}>> GetAllAsync() => await _context.Set<{entity}>().ToListAsync();{Environment.NewLine}{Environment.NewLine}    public async Task<PagedResult<{entity}>> ListAsync(int page = 1, int pageSize = 10){Environment.NewLine}    {{{Environment.NewLine}        var query = _context.Set<{entity}>();{Environment.NewLine}        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();{Environment.NewLine}        var total = await query.CountAsync();{Environment.NewLine}        return new PagedResult<{entity}>(items, total, page, pageSize);{Environment.NewLine}    }}{Environment.NewLine}{Environment.NewLine}    public Task UpdateAsync({entity} entity){Environment.NewLine}    {{{Environment.NewLine}        _context.Set<{entity}>().Update(entity);{Environment.NewLine}        return Task.CompletedTask;{Environment.NewLine}    }}{Environment.NewLine}";
                var idx = text.LastIndexOf("}");
                text = text.Insert(idx, methods);
            }
            var requiredUsings = new[]
            {
                "using System.Linq;",
                "using System.Threading.Tasks;",
                "using Microsoft.EntityFrameworkCore;",
                "using System.Collections.Generic;",
                "using " + solution + ".Core.Common.Models;",
                "using " + solution + ".Application.Common.Interfaces.Repositories;",
                "using " + solution + ".Core.Features." + plural + ";",
                "using " + solution + ".Infrastructure.Persistence;"
            };
            foreach (var u in requiredUsings)
                if (!text.Contains(u))
                    text = u + Environment.NewLine + text;
            File.WriteAllText(repoFile, text);
        }
    }
}

