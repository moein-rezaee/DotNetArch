using System;
using System.IO;

namespace DotNetArch.Core.Config;

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
            else if (key.Equals("port", StringComparison.OrdinalIgnoreCase))
                config.ApiPort = value;
            else if (key.Equals("framework", StringComparison.OrdinalIgnoreCase) || key.Equals("tfm", StringComparison.OrdinalIgnoreCase))
                config.TargetFramework = value;
            else if (key.Equals("layout", StringComparison.OrdinalIgnoreCase))
                config.Layout = value;
            else if (key.Equals("mcp", StringComparison.OrdinalIgnoreCase))
                config.McpEnabled = value.Equals("true", StringComparison.OrdinalIgnoreCase);
            else if (key.Equals("kit.prefix", StringComparison.OrdinalIgnoreCase))
                config.KitPrefix = value;
            else if (key.StartsWith("kit.", StringComparison.OrdinalIgnoreCase))
                config.Kits[key.Substring("kit.".Length)] = value;
            else if (key.Equals("ci.provider", StringComparison.OrdinalIgnoreCase))
                config.CiProvider = value;
            else if (key.Equals("git.host", StringComparison.OrdinalIgnoreCase))
                config.GitHost = value;
            else if (key.Equals("git.provider", StringComparison.OrdinalIgnoreCase))
                config.GitProvider = value;
            else if (key.Equals("docker.registry", StringComparison.OrdinalIgnoreCase))
                config.DockerRegistry = value;
            else if (key.Equals("nuget.source", StringComparison.OrdinalIgnoreCase))
                config.NuGetSource = value;
            else if (key.Equals("nuget.sourceName", StringComparison.OrdinalIgnoreCase))
                config.NuGetSourceName = value;
            else if (key.Equals("docker.image", StringComparison.OrdinalIgnoreCase))
                config.DockerImage = value;
            else if (key.Equals("docker.container", StringComparison.OrdinalIgnoreCase))
                config.DockerContainer = value;
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
            $"style: {config.ApiStyle}{nl}" +
            $"port: {config.ApiPort}{nl}" +
            $"framework: {config.TargetFramework}{nl}";
        if (config.IsV2)
            content += $"layout: {config.Layout}{nl}";
        if (!string.IsNullOrWhiteSpace(config.DatabaseProvider))
            content += $"database: {config.DatabaseProvider}{nl}";
        if (!string.IsNullOrWhiteSpace(config.DockerImage))
            content += $"docker.image: {config.DockerImage}{nl}";
        if (!string.IsNullOrWhiteSpace(config.DockerContainer))
            content += $"docker.container: {config.DockerContainer}{nl}";
        if (!string.IsNullOrWhiteSpace(config.DockerRegistry))
            content += $"docker.registry: {config.DockerRegistry}{nl}";
        if (!string.IsNullOrWhiteSpace(config.CiProvider))
            content += $"ci.provider: {config.CiProvider}{nl}";
        if (!string.IsNullOrWhiteSpace(config.GitProvider))
            content += $"git.provider: {config.GitProvider}{nl}";
        if (!string.IsNullOrWhiteSpace(config.GitHost))
            content += $"git.host: {config.GitHost}{nl}";
        if (!string.IsNullOrWhiteSpace(config.NuGetSource))
            content += $"nuget.source: {config.NuGetSource}{nl}";
        if (!string.IsNullOrWhiteSpace(config.NuGetSourceName))
            content += $"nuget.sourceName: {config.NuGetSourceName}{nl}";
        if (config.McpEnabled)
            content += $"mcp: true{nl}";
        if (!string.IsNullOrWhiteSpace(config.KitPrefix))
            content += $"kit.prefix: {config.KitPrefix}{nl}";
        foreach (var kit in config.Kits)
            content += $"kit.{kit.Key}: {kit.Value}{nl}";
        foreach (var kv in config.Entities)
        {
            var status = kv.Value.HasCrud && kv.Value.HasAction ? "both" : kv.Value.HasCrud ? "crud" : "action";
            content += $"entity.{kv.Key}: {status}{nl}";
        }
        File.WriteAllText(path, content);
    }
}
