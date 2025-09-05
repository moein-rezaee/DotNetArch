using System;
using System.IO;
using DotNetArch.Scaffolding;

static class ConstantScaffolder
{
    public static bool EntityExists(SolutionConfig config, string entity)
    {
        var solution = config.SolutionName;
        var plural = Naming.Pluralize(entity);
        var featureDir = Path.Combine(config.SolutionPath, $"{solution}.Core", "Features", plural);
        return Directory.Exists(featureDir);
    }

    public static bool Generate(SolutionConfig config, string? entity, string constantName)
    {
        if (string.IsNullOrWhiteSpace(config.SolutionName) ||
            string.IsNullOrWhiteSpace(constantName))
        {
            Program.Error("Solution and constant names are required.");
            return false;
        }

        var solution = config.SolutionName;
        string constantsDir;
        string @namespace;

        if (!string.IsNullOrWhiteSpace(entity))
        {
            if (!EntityExists(config, entity))
            {
                Program.Error($"Entity '{entity}' does not exist.");
                return false;
            }

            var plural = Naming.Pluralize(entity);
            var coreDir = Path.Combine(config.SolutionPath, $"{solution}.Core");
            var featureDir = Path.Combine(coreDir, "Features", plural);
            constantsDir = Path.Combine(featureDir, "Constants");
            @namespace = $"{solution}.Core.Features.{plural}.Constants";
        }
        else
        {
            var coreDir = Path.Combine(config.SolutionPath, $"{solution}.Core");
            constantsDir = Path.Combine(coreDir, "Common", "Constants");
            @namespace = $"{solution}.Core.Common.Constants";
        }

        Directory.CreateDirectory(constantsDir);

        constantName = Upper(constantName);
        var file = Path.Combine(constantsDir, constantName + ".cs");
        if (File.Exists(file))
        {
            var msg = string.IsNullOrWhiteSpace(entity)
                ? $"Constant '{constantName}' already exists."
                : $"Constant '{constantName}' already exists for entity '{entity}'.";
            Program.Error(msg);
            return false;
        }

        var content = $@"namespace {@namespace};

public static class {constantName}
{{
}}";
        File.WriteAllText(file, content);
        return true;
    }

    static string Upper(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text[1..];
}
