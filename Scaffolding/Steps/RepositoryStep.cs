using System;
using System.IO;
using DotNetArch.Scaffolding;

namespace DotNetArch.Scaffolding.Steps;

public class RepositoryStep : IScaffoldStep
{
    public void Execute(string solution, string entity, string provider, string basePath, string startupProject)
    {
        var plural = Naming.Pluralize(entity);
        var commonDir = Path.Combine(basePath, $"{solution}.Core", "Common");
        Directory.CreateDirectory(commonDir);
        var pagedResultFile = Path.Combine(commonDir, "PagedResult.cs");
        if (!File.Exists(pagedResultFile))
        {
            var pagedContent = """
using System.Collections.Generic;

namespace {{solution}}.Core.Common;

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);
""";
            File.WriteAllText(pagedResultFile, pagedContent.Replace("{{solution}}", solution));
        }

        var coreDir = Path.Combine(basePath, $"{solution}.Core", "Domain", plural);
        Directory.CreateDirectory(coreDir);
        var ifaceFile = Path.Combine(coreDir, $"I{entity}Repository.cs");
        var ifaceTemplate = """
using System.Threading.Tasks;
using System.Collections.Generic;
using {{solution}}.Core.Common;

namespace {{solution}}.Core.Domain.{{entities}};

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
                var insert = $@"    Task<{entity}?> GetByIdAsync(int id);
    Task<List<{entity}>> GetAllAsync();
    Task<PagedResult<{entity}>> ListAsync(int page = 1, int pageSize = 10);
    Task AddAsync({entity} entity);
    Task UpdateAsync({entity} entity);
    Task DeleteAsync({entity} entity);
";
                var idx = text.LastIndexOf("}");
                text = text.Insert(idx, insert);
            }
            if (!text.Contains("using " + solution + ".Core.Common;"))
                text = "using " + solution + ".Core.Common;" + Environment.NewLine + text;
            File.WriteAllText(ifaceFile, text);
        }

        var infraDir = Path.Combine(basePath, $"{solution}.Infrastructure", plural);
        Directory.CreateDirectory(infraDir);
        var repoFile = Path.Combine(infraDir, $"{entity}Repository.cs");
        var repoTemplate = """
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using {{solution}}.Core.Common;
using {{solution}}.Core.Domain.{{entities}};
using {{solution}}.Infrastructure.Persistence;

namespace {{solution}}.Infrastructure.{{entities}};

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
                var methods = $@"    public async Task AddAsync({entity} entity) => await _context.Set<{entity}>().AddAsync(entity);

    public async Task DeleteAsync({entity} entity)
    {{
        _context.Set<{entity}>().Remove(entity);
        await Task.CompletedTask;
    }}

    public async Task<{entity}?> GetByIdAsync(int id) => await _context.Set<{entity}>().FindAsync(id);

    public async Task<List<{entity}>> GetAllAsync() => await _context.Set<{entity}>().ToListAsync();

    public async Task<PagedResult<{entity}>> ListAsync(int page = 1, int pageSize = 10)
    {{
        var query = _context.Set<{entity}>();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var total = await query.CountAsync();
        return new PagedResult<{entity}>(items, total, page, pageSize);
    }}

    public Task UpdateAsync({entity} entity)
    {{
        _context.Set<{entity}>().Update(entity);
        return Task.CompletedTask;
    }}
";
                var idx = text.LastIndexOf("}");
                text = text.Insert(idx, methods);
            }
            var requiredUsings = new[]
            {
                "using System.Linq;",
                "using System.Threading.Tasks;",
                "using Microsoft.EntityFrameworkCore;",
                "using System.Collections.Generic;",
                "using " + solution + ".Core.Common;",
                "using " + solution + ".Core.Domain." + plural + ";",
                "using " + solution + ".Infrastructure.Persistence;"
            };
            foreach (var u in requiredUsings)
                if (!text.Contains(u))
                    text = u + Environment.NewLine + text;
            File.WriteAllText(repoFile, text);
        }
    }
}
