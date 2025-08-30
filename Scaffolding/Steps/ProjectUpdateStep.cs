using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DotNetArch.Scaffolding.Steps;

public class ProjectUpdateStep : IScaffoldStep
{
    public void Execute(string solution, string entity, string provider)
    {
        UpdateApplicationProject(solution);
        UpdateApiProject(solution, provider);
        UpdateProgram(solution, provider);
    }

    static void UpdateApplicationProject(string solution)
    {
        var appProj = Path.Combine($"{solution}.Application", $"{solution}.Application.csproj");
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

    static void UpdateApiProject(string solution, string provider)
    {
        var apiProj = Path.Combine($"{solution}.API", $"{solution}.API.csproj");
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

    static void UpdateProgram(string solution, string provider)
    {
        var programFile = Path.Combine($"{solution}.API", "Program.cs");
        if (!File.Exists(programFile)) return;
        var lines = File.ReadAllLines(programFile).ToList();
        foreach (var u in new []{"using MediatR;", "using FluentValidation;", "using FluentValidation.AspNetCore;", "using Microsoft.EntityFrameworkCore;", $"using {solution}.Infrastructure.Persistence;"})
        {
            if (!lines.Contains(u)) lines.Insert(0, u);
        }
        var idx = lines.FindIndex(l => l.Contains("var builder"));
        if (idx >= 0)
        {
            lines.Insert(idx + 1, provider == "SQLite" ?
                "builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(\"Data Source=app.db\"));" :
                "builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(\"Server=.;Database=AppDb;Trusted_Connection=True;\"));");
            lines.Insert(idx + 2, "builder.Services.AddMediatR(typeof(Program));");
            lines.Insert(idx + 3, "builder.Services.AddValidatorsFromAssemblyContaining<Program>();");
        }
        File.WriteAllLines(programFile, lines);
    }
}
