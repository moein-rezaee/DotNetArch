using System;
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
    // EF Core 9 is required to match the dotnet-ef tool installed by the scaffolded project
    private const string EfCoreVersion = "9.0.0";
    private const string SqlClientVersion = "5.2.1";
    private const string OpenApiVersion = "9.0.0";
    private const string SwaggerVersion = "6.5.0";

    public void Execute(SolutionConfig config, string entity)
    {
        var solution = config.SolutionName;
        var provider = config.DatabaseProvider;
        var basePath = config.SolutionPath;
        var startupProject = config.StartupProject;
        var apiStyle = config.ApiStyle;
        var infraPath = Path.Combine(basePath, $"{solution}.Infrastructure");
        var persistencePath = Path.Combine(infraPath, "Persistence");
        if (provider == "SQLite")
        {
            var dataDir = Path.Combine(persistencePath, "Data");
            Directory.CreateDirectory(dataDir);
        }
        Directory.CreateDirectory(Path.Combine(persistencePath, "Migrations"));
        UpdateApplicationProject(solution, basePath);
        UpdateInfrastructureProject(solution, basePath, provider);
        UpdateApiProject(solution, provider, basePath, startupProject);
        EnsureDependencyInjectionFiles(solution, basePath, provider);
        RemoveTemplateFiles(basePath, startupProject);
        EnsureConfigFiles(basePath, startupProject);
        UpdateProgram(solution, provider, entity, basePath, startupProject, apiStyle);
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

        // remove DI-specific MediatR package that shouldn't live in the Application project
        foreach (var pr in packages.Where(p =>
            {
                var include = (string?)p.Attribute("Include");
                return include == "MediatR.Extensions.Microsoft.DependencyInjection";
            }).ToList())
        {
            pr.Remove();
        }

        EnsurePackage(doc, "MediatR", MediatRVersion);
        EnsurePackage(doc, "FluentValidation", FluentValidationVersion);
        EnsurePackage(doc, "FluentValidation.DependencyInjectionExtensions", FluentValidationVersion);

        doc.Save(appProj);
    }

    static void UpdateInfrastructureProject(string solution, string basePath, string provider)
    {
        var infraProj = Path.Combine(basePath, $"{solution}.Infrastructure", $"{solution}.Infrastructure.csproj");
        if (!File.Exists(infraProj)) return;

        var doc = XDocument.Load(infraProj);

        EnsurePackage(doc, "Microsoft.EntityFrameworkCore", EfCoreVersion);
        EnsurePackage(doc, "Microsoft.EntityFrameworkCore.Design", EfCoreVersion);

        var providerPackage = provider == "SQLite"
            ? "Microsoft.EntityFrameworkCore.Sqlite"
            : "Microsoft.EntityFrameworkCore.SqlServer";
        EnsurePackage(doc, providerPackage, EfCoreVersion);
        if (provider != "SQLite")
            EnsurePackage(doc, "Microsoft.Data.SqlClient", SqlClientVersion);

        doc.Save(infraProj);
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
        EnsurePackage(doc, "Microsoft.EntityFrameworkCore.Design", EfCoreVersion);
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
        EnsureProjectReference(doc, $"{rel}{solution}.Core{Path.DirectorySeparatorChar}{solution}.Core.csproj");
        EnsureProjectReference(doc, $"{rel}{solution}.Application{Path.DirectorySeparatorChar}{solution}.Application.csproj");
        EnsureProjectReference(doc, $"{rel}{solution}.Infrastructure{Path.DirectorySeparatorChar}{solution}.Infrastructure.csproj");

        doc.Save(apiProj);
    }

    static void EnsureDependencyInjectionFiles(string solution, string basePath, string provider)
    {
        var appDir = Path.Combine(basePath, $"{solution}.Application");
        Directory.CreateDirectory(appDir);
        var appDi = Path.Combine(appDir, "DependencyInjection.cs");
        if (!File.Exists(appDi))
        {
            var appContent = """
using MediatR;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace {{solution}}.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<AssemblyMarker>());
        services.AddValidatorsFromAssemblyContaining<AssemblyMarker>();
        return services;
    }
}
""";
            File.WriteAllText(appDi, appContent.Replace("{{solution}}", solution));
        }
        else
        {
            var lines = File.ReadAllLines(appDi).ToList();
            if (!lines.Any(l => l.Contains("using FluentValidation")))
            {
                var insertIndex = lines.FindLastIndex(l => l.StartsWith("using ")) + 1;
                if (insertIndex < 0) insertIndex = 0;
                lines.Insert(insertIndex, "using FluentValidation;");
            }
            if (!lines.Any(l => l.Contains("AddValidatorsFromAssemblyContaining")))
            {
                var idx = lines.FindIndex(l => l.Contains("AddMediatR"));
                if (idx != -1)
                {
                    lines.Insert(idx + 1, "        services.AddValidatorsFromAssemblyContaining<AssemblyMarker>();");
                }
            }
            File.WriteAllLines(appDi, lines);
        }

        var infraDir = Path.Combine(basePath, $"{solution}.Infrastructure");
        Directory.CreateDirectory(infraDir);
        var infraDi = Path.Combine(infraDir, "DependencyInjection.cs");
        if (!File.Exists(infraDi))
        {
            string infraContent;
            if (provider == "SQLite")
            {
                infraContent = """
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using {{solution}}.Infrastructure.Persistence;

namespace {{solution}}.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, "..", "{{solution}}.Infrastructure", "Persistence", "Data", "app.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
        return services;
    }
}
""";
            }
            else
            {
                infraContent = """
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using {{solution}}.Infrastructure.Persistence;

namespace {{solution}}.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer("Server=.;Database=AppDb;Trusted_Connection=True;"));
        return services;
    }
}
""";
            }
            File.WriteAllText(infraDi, infraContent.Replace("{{solution}}", solution));
        }
        // ensure design-time factory for EF Core so migrations can run without full host
        var persistenceDir = Path.Combine(infraDir, "Persistence");
        Directory.CreateDirectory(persistenceDir);
        var factoryFile = Path.Combine(persistenceDir, "AppDbContextFactory.cs");
        if (!File.Exists(factoryFile))
        {
            string factoryContent;
            if (provider == "SQLite")
            {
                factoryContent = """
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace {{solution}}.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, "..", "{{solution}}.Infrastructure", "Persistence", "Data", "app.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        return new AppDbContext(options);
    }
}
""";
            }
            else
            {
                factoryContent = """
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace {{solution}}.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=AppDb;Trusted_Connection=True;")
            .Options;
        return new AppDbContext(options);
    }
}
""";
            }
            File.WriteAllText(factoryFile, factoryContent.Replace("{{solution}}", solution));
        }
    }

    static void UpdateProgram(string solution, string provider, string entity, string basePath, string startupProject, string apiStyle)
    {
        var programFile = Path.Combine(basePath, startupProject, "Program.cs");
        if (!File.Exists(programFile)) return;
        var lines = File.ReadAllLines(programFile).ToList();

        lines.RemoveAll(l => l.Contains("FluentValidation.AspNetCore"));
        lines.RemoveAll(l => l.Contains("AddFluentValidationAutoValidation"));
        lines.RemoveAll(l => l.Contains("AddFluentValidationClientsideAdapters"));
        lines.RemoveAll(l => l.TrimStart().StartsWith("using ") && l.Contains(".."));

        var usingLines = new List<string>
        {
            "using System;",
            "using System.IO;",
            "using DotNetEnv;",
            "using Microsoft.Extensions.Configuration;",
            "using Microsoft.Extensions.DependencyInjection;",
            $"using {solution}.Application;",
            $"using {solution}.Infrastructure;"
        };
        if (!string.IsNullOrWhiteSpace(entity))
        {
            var plural = Naming.Pluralize(entity);
            usingLines.Add("using Microsoft.EntityFrameworkCore;");
            usingLines.Add($"using {solution}.Infrastructure.Persistence;");
            if (apiStyle == "fast")
                usingLines.Add($"using {startupProject}.Features.{plural};");
        }
        foreach (var u in usingLines)
            if (!lines.Any(l => l.Trim() == u))
                lines.Insert(0, u);

        var idx = lines.FindIndex(l => l.Contains("var builder"));
        if (idx >= 0)
        {
            if (!lines.Any(l => l.Contains("ASPNETCORE_ENVIRONMENT")))
            {
                lines.Insert(idx, "var env = Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\") ?? \"development\";");
                idx++;
            }
            var insertIndex = idx + 1;
            if (!lines.Any(l => l.Contains("DotNetEnv.Env.Load")))
                lines.Insert(insertIndex++, "DotNetEnv.Env.Load(Path.Combine(Directory.GetCurrentDirectory(), \"config\", \"env\", $\".env.{env.ToLower()}\"));");
            if (!lines.Any(l => l.Contains("appsettings.{env.ToLower()}")))
                lines.Insert(insertIndex++, "builder.Configuration.AddJsonFile(Path.Combine(\"config\", \"settings\", $\"appsettings.{env.ToLower()}.json\"), optional: true, reloadOnChange: true);");
            if (!lines.Any(l => l.Contains("AddEndpointsApiExplorer")))
                lines.Insert(insertIndex++, "builder.Services.AddEndpointsApiExplorer();");
            if (!lines.Any(l => l.Contains("AddSwaggerGen")))
                lines.Insert(insertIndex++, "builder.Services.AddSwaggerGen();");
            if (apiStyle == "controller")
            {
                if (!lines.Any(l => l.Contains("AddControllers")))
                    lines.Insert(insertIndex++, "builder.Services.AddControllers();");
            }
            else
            {
                lines.RemoveAll(l => l.Contains("AddControllers"));
            }
            if (!lines.Any(l => l.Contains("AddApplication()")))
                lines.Insert(insertIndex++, "builder.Services.AddApplication();");
            if (!lines.Any(l => l.Contains("AddInfrastructure")))
                lines.Insert(insertIndex++, "builder.Services.AddInfrastructure(builder.Configuration);");
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
            if (apiStyle == "controller" && !lines.Any(l => l.Contains("app.MapControllers()")))
                lines.Insert(runIdx++, "app.MapControllers();");
            if (apiStyle == "fast")
            {
                lines.RemoveAll(l => l.Contains("app.MapControllers()"));
                if (!string.IsNullOrWhiteSpace(entity) && !lines.Any(l => l.Contains($"app.Map{entity}Endpoints")))
                    lines.Insert(runIdx++, $"app.Map{entity}Endpoints();");
            }
            if (!string.IsNullOrWhiteSpace(entity) && !lines.Any(l => l.Contains("Database.Migrate") || l.Contains("Database.EnsureCreated")))
            {
                var migrateLines = new List<string>
                {
                    "using (var scope = app.Services.CreateScope())",
                    "{",
                    "    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();",
                    "    if (db.Database.GetPendingMigrations().Any())",
                    "        db.Database.Migrate();",
                    "    else",
                    "        db.Database.EnsureCreated();",
                    "}"
                };
                foreach (var ml in migrateLines)
                    lines.Insert(runIdx++, ml);
            }
        }
        File.WriteAllLines(programFile, lines);
    }
    static void EnsureConfigFiles(string basePath, string startupProject)
    {
        var configDir = Path.Combine(basePath, startupProject, "config");
        var envDir = Path.Combine(configDir, "env");
        var settingsDir = Path.Combine(configDir, "settings");
        Directory.CreateDirectory(envDir);
        Directory.CreateDirectory(settingsDir);
        var proj = Path.Combine(basePath, startupProject, $"{startupProject}.csproj");
        if (File.Exists(proj))
            Program.RunCommand($"dotnet add {proj} package DotNetEnv", basePath, print: false);
        var defaultApp = Path.Combine(basePath, startupProject, "appsettings.json");
        if (File.Exists(defaultApp)) File.Delete(defaultApp);
        foreach (var env in new[] { "development", "test", "production" })
        {
            var envPath = Path.Combine(envDir, $".env.{env}");
            if (!File.Exists(envPath)) File.WriteAllText(envPath, string.Empty);
            var appPath = Path.Combine(settingsDir, $"appsettings.{env}.json");
            if (!File.Exists(appPath)) File.WriteAllText(appPath, "{}");
        }
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
