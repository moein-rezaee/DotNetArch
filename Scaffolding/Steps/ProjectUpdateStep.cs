using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DotNetArch.Scaffolding.Steps;

public class ProjectUpdateStep : IScaffoldStep
{
    private const string MediatRVersion = "12.2.0";
    private const string FluentValidationVersion = "11.9.0";
    private const string EfCoreVersion = "8.0.0";
    private const string SqlClientVersion = "5.1.2";

    public void Execute(string solution, string entity, string provider, string basePath, string startupProject)
    {
        UpdateApplicationProject(solution, basePath);
        UpdateInfrastructureProject(solution, basePath);
        UpdateApiProject(solution, provider, basePath, startupProject);
        UpdateProgram(solution, provider, basePath, startupProject);
    }

    static void EnsurePackage(XDocument doc, string include, string version)
    {
        var refs = doc.Root!.Elements("ItemGroup").Elements("PackageReference")
            .Where(p => (string?)p.Attribute("Include") == include).ToList();

        if (refs.Count == 0)
        {
            var group = new XElement("ItemGroup",
                new XElement("PackageReference",
                    new XAttribute("Include", include),
                    new XAttribute("Version", version)));
            doc.Root.Add(group);
        }
        else
        {
            refs[0].SetAttributeValue("Version", version);
            foreach (var extra in refs.Skip(1).ToList()) extra.Remove();
        }
    }

    static void UpdateApplicationProject(string solution, string basePath)
    {
        var appProj = Path.Combine(basePath, $"{solution}.Application", $"{solution}.Application.csproj");
        if (!File.Exists(appProj)) return;

        var doc = XDocument.Load(appProj);
        var packages = doc.Root!.Elements("ItemGroup").Elements("PackageReference");

        // remove DI-specific packages that shouldn't live in the Application project
        foreach (var pr in packages.Where(p =>
            {
                var include = (string?)p.Attribute("Include");
                return include == "MediatR.Extensions.Microsoft.DependencyInjection" ||
                       include == "FluentValidation.DependencyInjectionExtensions";
            }).ToList())
        {
            pr.Remove();
        }

        EnsurePackage(doc, "MediatR", MediatRVersion);
        EnsurePackage(doc, "FluentValidation", FluentValidationVersion);

        doc.Save(appProj);
    }

    static void UpdateInfrastructureProject(string solution, string basePath)
    {
        var infraProj = Path.Combine(basePath, $"{solution}.Infrastructure", $"{solution}.Infrastructure.csproj");
        if (!File.Exists(infraProj)) return;
        var text = File.ReadAllText(infraProj);

        if (text.Contains("Microsoft.EntityFrameworkCore"))
            text = Regex.Replace(
                text,
                @"<PackageReference Include=""Microsoft.EntityFrameworkCore"" Version=""[^""]+"" />",
                $"<PackageReference Include=\"Microsoft.EntityFrameworkCore\" Version=\"{EfCoreVersion}\" />");
        else
        {
            var insert =
                "  <ItemGroup>\n" +
                $"    <PackageReference Include=\"Microsoft.EntityFrameworkCore\" Version=\"{EfCoreVersion}\" />\n" +
                "  </ItemGroup>\n";
            text = text.Replace("</Project>", insert + "</Project>");
        }

        File.WriteAllText(infraProj, text);
    }

    static void UpdateApiProject(string solution, string provider, string basePath, string startupProject)
    {
        var apiProj = Path.Combine(basePath, startupProject, $"{startupProject}.csproj");
        if (!File.Exists(apiProj)) return;

        var doc = XDocument.Load(apiProj);

        EnsurePackage(doc, "MediatR", MediatRVersion);
        EnsurePackage(doc, "MediatR.Extensions.Microsoft.DependencyInjection", MediatRVersion);
        EnsurePackage(doc, "FluentValidation.DependencyInjectionExtensions", FluentValidationVersion);
        var providerPackage = provider == "SQLite"
            ? "Microsoft.EntityFrameworkCore.Sqlite"
            : "Microsoft.EntityFrameworkCore.SqlServer";
        EnsurePackage(doc, providerPackage, EfCoreVersion);
        if (provider != "SQLite")
            EnsurePackage(doc, "Microsoft.Data.SqlClient", SqlClientVersion);

        doc.Save(apiProj);
    }

    static void UpdateProgram(string solution, string provider, string basePath, string startupProject)
    {
        var programFile = Path.Combine(basePath, startupProject, "Program.cs");
        if (!File.Exists(programFile)) return;
        var lines = File.ReadAllLines(programFile).ToList();
        foreach (var u in new[]
        {
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
