using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotNetArch.Scaffolding;

namespace DotNetArch.Scaffolding.Steps;

public class ProjectUpdateStep : IScaffoldStep
{
    private const string MediatRVersion = "12.1.1";
    private const string FluentValidationVersion = "11.9.0";
    private const string EfCoreVersion = "8.0.0";
    private const string SqlClientVersion = "5.2.1";
    private const string OpenApiVersion = "9.0.0";
    private const string SwaggerVersion = "6.5.0";

    public void Execute(string solution, string entity, string provider, string basePath, string startupProject)
    {
        UpdateApplicationProject(solution, basePath);
        UpdateInfrastructureProject(solution, basePath);
        UpdateApiProject(solution, provider, basePath, startupProject);
        RemoveTemplateFiles(basePath, startupProject);
        UpdateProgram(solution, provider, entity, basePath, startupProject);
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
            var first = refs[0];
            var attr = first.Attribute("Version");
            var elem = first.Element("Version");
            if (attr != null) attr.Value = version;
            else if (elem != null) elem.Value = version;
            else first.Add(new XAttribute("Version", version));

            foreach (var extra in refs.Skip(1).ToList()) extra.Remove();
        }
    }

    static void EnsureProjectReference(XDocument doc, string include)
    {
        var refs = doc.Root!.Elements("ItemGroup").Elements("ProjectReference")
            .Where(p => (string?)p.Attribute("Include") == include).ToList();
        if (refs.Count == 0)
        {
            var group = doc.Root.Elements("ItemGroup").FirstOrDefault();
            if (group == null)
            {
                group = new XElement("ItemGroup");
                doc.Root.Add(group);
            }
            group.Add(new XElement("ProjectReference", new XAttribute("Include", include)));
        }
        else
        {
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
        EnsurePackage(doc, "FluentValidation.DependencyInjectionExtensions", FluentValidationVersion);
        EnsurePackage(doc, "Microsoft.AspNetCore.OpenApi", OpenApiVersion);
        EnsurePackage(doc, "Swashbuckle.AspNetCore", SwaggerVersion);
        foreach (var old in doc.Root!.Elements("ItemGroup").Elements("PackageReference")
                     .Where(p => (string?)p.Attribute("Include") == "MediatR.Extensions.Microsoft.DependencyInjection").ToList())
            old.Remove();
        var providerPackage = provider == "SQLite"
            ? "Microsoft.EntityFrameworkCore.Sqlite"
            : "Microsoft.EntityFrameworkCore.SqlServer";
        EnsurePackage(doc, providerPackage, EfCoreVersion);
        if (provider != "SQLite")
            EnsurePackage(doc, "Microsoft.Data.SqlClient", SqlClientVersion);

        var rel = ".." + Path.DirectorySeparatorChar;
        EnsureProjectReference(doc, $"{rel}{solution}.Application{Path.DirectorySeparatorChar}{solution}.Application.csproj");
        EnsureProjectReference(doc, $"{rel}{solution}.Infrastructure{Path.DirectorySeparatorChar}{solution}.Infrastructure.csproj");

        doc.Save(apiProj);
    }

    static void UpdateProgram(string solution, string provider, string entity, string basePath, string startupProject)
    {
        var programFile = Path.Combine(basePath, startupProject, "Program.cs");
        if (!File.Exists(programFile)) return;
        var lines = File.ReadAllLines(programFile).ToList();

        // remove leftover FluentValidation.AspNetCore references from template
        lines.RemoveAll(l => l.Contains("FluentValidation.AspNetCore"));
        lines.RemoveAll(l => l.Contains("AddFluentValidationAutoValidation"));
        lines.RemoveAll(l => l.Contains("AddFluentValidationClientsideAdapters"));
        // clean up malformed namespace lines (e.g., double dots)
        lines.RemoveAll(l => l.Contains(".."));
        var usingLines = new List<string>
        {
            "using System;",
            "using MediatR;",
            "using FluentValidation;",
            $"using {solution}.Application;",
            "using Microsoft.Extensions.DependencyInjection;"
        };

        if (!string.IsNullOrWhiteSpace(entity))
        {
            var plural = Naming.Pluralize(entity);
            usingLines.Add("using Microsoft.EntityFrameworkCore;");
            usingLines.Add($"using {solution}.Infrastructure.Persistence;");
            usingLines.Add($"using {solution}.Core.Interfaces;");
            usingLines.Add($"using {solution}.Infrastructure;");
            usingLines.Add($"using {solution}.Core.Domain.{plural};");
            usingLines.Add($"using {solution}.Infrastructure.{plural};");
        }

        foreach (var u in usingLines)
        {
            if (!lines.Any(l => l.Trim() == u))
                lines.Insert(0, u);
        }
        var idx = lines.FindIndex(l => l.Contains("var builder"));
        if (idx >= 0)
        {
            var insertIndex = idx + 1;
            var dbLine = provider == "SQLite"
                ? "builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(\"Data Source=app.db\"));"
                : "builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(\"Server=.;Database=AppDb;Trusted_Connection=True;\"));";
            if (!lines.Any(l => l.Contains("AddEndpointsApiExplorer")))
                lines.Insert(insertIndex++, "builder.Services.AddEndpointsApiExplorer();");
            if (!lines.Any(l => l.Contains("AddSwaggerGen")))
                lines.Insert(insertIndex++, "builder.Services.AddSwaggerGen();");
            if (!lines.Any(l => l.Contains("AddControllers")))
                lines.Insert(insertIndex++, "builder.Services.AddControllers();");
            if (!string.IsNullOrWhiteSpace(entity) && !lines.Any(l => l.Contains("AddDbContext<AppDbContext>")))
                lines.Insert(insertIndex++, dbLine);
            if (!lines.Any(l => l.Contains("RegisterServicesFromAssemblyContaining<AssemblyMarker>")))
                lines.Insert(insertIndex++, "builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<AssemblyMarker>());");
            if (!lines.Any(l => l.Contains("AddValidatorsFromAssemblyContaining<AssemblyMarker>")))
                lines.Insert(insertIndex++, "builder.Services.AddValidatorsFromAssemblyContaining<AssemblyMarker>();");
            if (!string.IsNullOrWhiteSpace(entity))
            {
                if (!lines.Any(l => l.Contains($"AddScoped<I{entity}Repository, {entity}Repository>()")))
                    lines.Insert(insertIndex++, $"builder.Services.AddScoped<I{entity}Repository, {entity}Repository>();");
                if (!lines.Any(l => l.Contains("AddScoped<IUnitOfWork, UnitOfWork>()")))
                    lines.Insert(insertIndex++, "builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();");
            }
        }

        var buildIdx = lines.FindIndex(l => l.Contains("var app = builder.Build();"));
        if (buildIdx >= 0 && !lines.Any(l => l.Contains("UseSwaggerUI()")))
        {
            var insertIndex = buildIdx + 1;
            lines.Insert(insertIndex++, "if (app.Environment.IsDevelopment())");
            lines.Insert(insertIndex++, "{");
            lines.Insert(insertIndex++, "    app.UseSwagger();");
            lines.Insert(insertIndex++, "    app.UseSwaggerUI();");
            lines.Insert(insertIndex++, "}");
        }
        var runIdx = lines.FindIndex(l => l.Contains("app.Run();"));
        if (runIdx >= 0)
        {
            if (!lines.Any(l => l.Contains("app.MapControllers()")))
                lines.Insert(runIdx++, "app.MapControllers();");
            if (!string.IsNullOrWhiteSpace(entity) && !lines.Any(l => l.Contains("Database.EnsureCreated")))
            {
                var ensureLines = new List<string>
                {
                    "using (var scope = app.Services.CreateScope())",
                    "{",
                    "    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();",
                    "    db.Database.EnsureCreated();",
                    "}"
                };
                foreach (var el in ensureLines)
                {
                    lines.Insert(runIdx++, el);
                }
            }
        }
        File.WriteAllLines(programFile, lines);
    }

    static void RemoveTemplateFiles(string basePath, string startupProject)
    {
        var weather = Path.Combine(basePath, startupProject, "WeatherForecast.cs");
        if (File.Exists(weather)) File.Delete(weather);
        var controller = Path.Combine(basePath, startupProject, "Controllers", "WeatherForecastController.cs");
        if (File.Exists(controller)) File.Delete(controller);

        var program = Path.Combine(basePath, startupProject, "Program.cs");
        if (File.Exists(program))
        {
            var lines = File.ReadAllLines(program).ToList();
            var start = lines.FindIndex(l => l.Contains("var summaries"));
            if (start >= 0)
            {
                var end = lines.FindIndex(start, l => l.Contains("GetWeatherForecast"));
                if (end >= start)
                    lines.RemoveRange(start, end - start + 1);
            }
            var recordIndex = lines.FindIndex(l => l.Contains("record WeatherForecast"));
            if (recordIndex >= 0)
            {
                var closeIndex = lines.FindIndex(recordIndex, l => l.Trim() == "}");
                if (closeIndex >= recordIndex)
                    lines.RemoveRange(recordIndex, closeIndex - recordIndex + 1);
            }
            File.WriteAllLines(program, lines);
        }
    }
}
