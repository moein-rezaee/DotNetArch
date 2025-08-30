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
        var content = $"solution: {config.SolutionName}\npath: {config.SolutionPath}\nstartup: {config.StartupProject}\n";
        File.WriteAllText(path, content);
    }
}
