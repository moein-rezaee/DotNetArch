using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DotNetArch.Scaffolding.Steps;

public class ProjectUpdateStep : IScaffoldStep
{
    public void Execute(string solution, string entity, string provider, string basePath, string startupProject)
    {
        UpdateApplicationProject(solution, basePath);
        UpdateInfrastructureProject(solution, basePath);
        UpdateApiProject(solution, provider, basePath, startupProject);
        UpdateProgram(solution, provider, basePath, startupProject);
    }

    static void UpdateApplicationProject(string solution, string basePath)
    {
        var appProj = Path.Combine(basePath, $"{solution}.Application", $"{solution}.Application.csproj");
        if (!File.Exists(appProj)) return;
        var text = File.ReadAllText(appProj);
        if (text.Contains("MediatR")) return;
        var insert = $$"""
  <ItemGroup>
    <PackageReference Include="MediatR" Version="12.1.1" />
    <PackageReference Include="FluentValidation" Version="11.9.0" />
  </ItemGroup>
""";
        text = text.Replace("</Project>", insert + "</Project>");
        File.WriteAllText(appProj, text);
    }

    static void UpdateInfrastructureProject(string solution, string basePath)
    {
        var infraProj = Path.Combine(basePath, $"{solution}.Infrastructure", $"{solution}.Infrastructure.csproj");
        if (!File.Exists(infraProj)) return;
        var text = File.ReadAllText(infraProj);
        if (text.Contains("Microsoft.EntityFrameworkCore")) return;
        var insert = $$"""
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="7.0.0" />
  </ItemGroup>
""";
        text = text.Replace("</Project>", insert + "</Project>");
        File.WriteAllText(infraProj, text);
    }

    static void UpdateApiProject(string solution, string provider, string basePath, string startupProject)
    {
        var apiProj = Path.Combine(basePath, startupProject, $"{startupProject}.csproj");
        if (!File.Exists(apiProj)) return;
        var text = File.ReadAllText(apiProj);
        if (text.Contains("MediatR.Extensions")) return;
        var providerPackage = provider == "SQLite"
            ? "<PackageReference Include=\"Microsoft.EntityFrameworkCore.Sqlite\" Version=\"7.0.0\" />"
            : "<PackageReference Include=\"Microsoft.EntityFrameworkCore.SqlServer\" Version=\"7.0.0\" />";
        var insert = $$"""
  <ItemGroup>
    <PackageReference Include="MediatR.Extensions.Microsoft.DependencyInjection" Version="12.1.1" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.9.0" />
    {{providerPackage}}
  </ItemGroup>
""";
        text = text.Replace("</Project>", insert + "</Project>");
        File.WriteAllText(apiProj, text);
    }

    static void UpdateProgram(string solution, string provider, string basePath, string startupProject)
    {
        var programFile = Path.Combine(basePath, startupProject, "Program.cs");
        if (!File.Exists(programFile)) return;
        var lines = File.ReadAllLines(programFile).ToList();
        foreach (var u in new []{
            "using MediatR;",
            "using FluentValidation;",
            "using FluentValidation.AspNetCore;",
            "using Microsoft.EntityFrameworkCore;",
            $"using {solution}.Infrastructure.Persistence;",
            $"using {solution}.Core.Interfaces;",
            $"using {solution}.Infrastructure;"
        })
        {
            if (!lines.Contains(u)) lines.Insert(0, u);
        }
        var idx = lines.FindIndex(l => l.Contains("var builder"));
        if (idx >= 0)
        {
            var insertIndex = idx + 1;
            var dbLine = provider == "SQLite"
                ? "builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(\"Data Source=app.db\"));"
                : "builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(\"Server=.;Database=AppDb;Trusted_Connection=True;\"));";
            if (!lines.Any(l => l.Contains("AddDbContext<AppDbContext>")))
                lines.Insert(insertIndex++, dbLine);
            if (!lines.Any(l => l.Contains("AddMediatR")))
                lines.Insert(insertIndex++, "builder.Services.AddMediatR(typeof(Program));");
            if (!lines.Any(l => l.Contains("AddValidatorsFromAssemblyContaining<Program>")))
                lines.Insert(insertIndex++, "builder.Services.AddValidatorsFromAssemblyContaining<Program>();");
            if (!lines.Any(l => l.Contains("AddScoped<IUnitOfWork, UnitOfWork>()")))
                lines.Insert(insertIndex, "builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();");
        }
        File.WriteAllLines(programFile, lines);
    }
}
