using System.Text.RegularExpressions;

namespace DotNetArch.Core.Scaffolding.Solution;

/// <summary>EF Core migration orchestration shared by CRUD/action generation, <c>exec</c> and <c>remove migration</c>.</summary>
public static class MigrationService
{
    private static bool HasMigrations(SolutionConfig config) =>
        !string.IsNullOrWhiteSpace(config.DatabaseProvider)
        && !config.DatabaseProvider.Equals("Mongo", StringComparison.OrdinalIgnoreCase)
        && !config.DatabaseProvider.Equals("None", StringComparison.OrdinalIgnoreCase);

    private static (string Infra, string Startup) Projects(SolutionConfig config) =>
        ($"{config.SolutionName}.Infrastructure/{config.SolutionName}.Infrastructure.csproj",
         $"{config.StartupProject}/{config.StartupProject}.csproj");

    /// <summary>Build, add an <c>Auto_{entity}_{timestamp}</c> migration and apply it (used after crud/action generation).</summary>
    public static void BuildAddAndApply(SolutionConfig config, string entity)
    {
        var (infraProj, startProj) = Projects(config);
        if (!ToolHost.RunCommand("dotnet build", config.SolutionPath))
        {
            ToolHost.Error("Build failed; skipping migrations.");
            return;
        }

        var migName = $"Auto_{entity}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        if (ToolHost.RunCommand($"dotnet ef migrations add {migName} --project {infraProj} --startup-project {startProj} --output-dir {PathConstants.MigrationsRelativePath}", config.SolutionPath))
            ToolHost.RunCommand($"dotnet ef database update --project {infraProj} --startup-project {startProj}", config.SolutionPath);
    }

    /// <summary>Build, add an <c>Auto_{timestamp}</c> migration when the model changed, and update the database (used by <c>exec</c>).</summary>
    public static void UpdateMigrations(SolutionConfig config, string basePath)
    {
        if (!HasMigrations(config))
        {
            ToolHost.Info("No migrations for the selected provider.");
            return;
        }

        if (!SolutionTooling.EnsureEfTool(basePath))
            return;

        if (!ToolHost.RunCommand("dotnet build", basePath))
        {
            ToolHost.Error("Build failed; skipping migrations.");
            return;
        }

        var (infraProj, startProj) = Projects(config);
        var migName = $"Auto_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var (success, output) = ToolHost.RunCommandCapture($"dotnet ef migrations add {migName} --project {infraProj} --startup-project {startProj} --output-dir {PathConstants.MigrationsRelativePath}", basePath);
        var proceed = true;
        if (!success)
        {
            if (output.Contains("No changes were detected", StringComparison.OrdinalIgnoreCase))
            {
                ToolHost.Info("No changes were detected.");
            }
            else
            {
                ToolHost.Error(output.Trim());
                proceed = false;
            }
        }

        if (!proceed)
            return;

        var (dbSuccess, dbOutput) = ToolHost.RunCommandCapture($"dotnet ef database update --project {infraProj} --startup-project {startProj}", basePath);
        if (!dbSuccess)
            ToolHost.Error(dbOutput.Trim());
        else if (dbOutput.Contains("No migrations were applied", StringComparison.OrdinalIgnoreCase))
            ToolHost.Info("Database already up to date.");
    }

    public static string[] ListMigrations(string infraProj, string startProj, string basePath)
    {
        var (success, output) = ToolHost.RunCommandCapture($"dotnet ef migrations list --project {infraProj} --startup-project {startProj} --no-build", basePath);
        if (!success)
            return Array.Empty<string>();

        var regex = new Regex("^\\d+_");
        var list = new List<string>();
        foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!regex.IsMatch(trimmed))
                continue;
            var space = trimmed.IndexOf(' ');
            if (space >= 0)
                trimmed = trimmed[..space];
            list.Add(trimmed);
        }
        return list.ToArray();
    }

    /// <summary>Rolls the database back one migration and removes the last migration (the <c>remove migration</c> command).</summary>
    public static bool RemoveLast(SolutionConfig config, string basePath)
    {
        var provider = config.DatabaseProvider;
        if (string.IsNullOrWhiteSpace(provider) || provider.Equals("Mongo", StringComparison.OrdinalIgnoreCase))
        {
            ToolHost.Info("No migrations to remove for the selected provider.");
            return true;
        }

        if (!SolutionTooling.EnsureEfTool(basePath))
            return false;

        var (infraProj, startProj) = Projects(config);
        var migrations = ListMigrations(infraProj, startProj, basePath);
        if (migrations.Length == 0)
        {
            ToolHost.Info("No migrations found.");
            return true;
        }

        var prev = migrations.Length > 1 ? migrations[^2] : "0";
        if (!ToolHost.RunCommand($"dotnet ef database update {prev} --project {infraProj} --startup-project {startProj} --no-build", basePath))
        {
            ToolHost.Error("Failed to rollback database; migration removal aborted.");
            return false;
        }

        if (!ToolHost.RunCommand($"dotnet ef migrations remove --force --project {infraProj} --startup-project {startProj} --no-build", basePath))
        {
            ToolHost.Error("Failed to remove migration.");
            return false;
        }

        ToolHost.RunCommand("dotnet build", basePath);
        return true;
    }
}
