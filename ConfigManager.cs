using System;
using System.IO;

public static class ConfigManager
{
    private const string FileName = "dotnet-arch.yml";

    public static SolutionConfig? Load(string basePath)
    {
        var path = Path.Combine(basePath, FileName);
        if (!File.Exists(path))
            return null;
        var lines = File.ReadAllLines(path);
        var config = new SolutionConfig();
        foreach (var line in lines)
        {
            var parts = line.Split(':', 2);
            if (parts.Length != 2) continue;
            var key = parts[0].Trim();
            var value = parts[1].Trim();
            if (key.Equals("solution", StringComparison.OrdinalIgnoreCase))
                config.SolutionName = value;
            else if (key.Equals("path", StringComparison.OrdinalIgnoreCase))
                config.SolutionPath = value;
            else if (key.Equals("startup", StringComparison.OrdinalIgnoreCase))
                config.StartupProject = value;
            else if (key.Equals("database", StringComparison.OrdinalIgnoreCase))
                config.DatabaseProvider = value;
            else if (key.Equals("style", StringComparison.OrdinalIgnoreCase))
                config.ApiStyle = value;
            else if (key.StartsWith("entity.", StringComparison.OrdinalIgnoreCase))
            {
                var name = key.Substring("entity.".Length);
                var state = new EntityStatus
                {
                    HasCrud = value.Equals("crud", StringComparison.OrdinalIgnoreCase) || value.Equals("both", StringComparison.OrdinalIgnoreCase),
                    HasAction = value.Equals("action", StringComparison.OrdinalIgnoreCase) || value.Equals("both", StringComparison.OrdinalIgnoreCase)
                };
                config.Entities[name] = state;
            }
        }
        if (string.IsNullOrWhiteSpace(config.SolutionPath))
            config.SolutionPath = basePath;
        if (string.IsNullOrWhiteSpace(config.StartupProject))
            config.StartupProject = $"{config.SolutionName}.API";
        return config;
    }

    public static void Save(string basePath, SolutionConfig config)
    {
        var path = Path.Combine(basePath, FileName);
        var nl = Environment.NewLine;
        var content =
            $"solution: {config.SolutionName}{nl}" +
            $"path: {config.SolutionPath}{nl}" +
            $"startup: {config.StartupProject}{nl}" +
            $"style: {config.ApiStyle}{nl}";
        if (!string.IsNullOrWhiteSpace(config.DatabaseProvider))
            content += $"database: {config.DatabaseProvider}{nl}";
        foreach (var kv in config.Entities)
        {
            var status = kv.Value.HasCrud && kv.Value.HasAction ? "both" : kv.Value.HasCrud ? "crud" : "action";
            content += $"entity.{kv.Key}: {status}{nl}";
        }
        File.WriteAllText(path, content);
    }
}
