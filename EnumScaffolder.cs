using System;
using System.IO;
using DotNetArch.Scaffolding;

static class EnumScaffolder
{
    public static bool Generate(SolutionConfig config, string entity, string enumName)
    {
        if (string.IsNullOrWhiteSpace(config.SolutionName) ||
            string.IsNullOrWhiteSpace(entity) ||
            string.IsNullOrWhiteSpace(enumName))
        {
            Program.Error("Solution, entity and enum names are required.");
            return false;
        }

        var solution = config.SolutionName;
        var plural = Naming.Pluralize(entity);
        var coreDir = Path.Combine(config.SolutionPath, $"{solution}.Core");
        var featureDir = Path.Combine(coreDir, "Features", plural);
        if (!Directory.Exists(featureDir))
        {
            Program.Error($"Entity '{entity}' does not exist.");
            return false;
        }

        var enumsDir = Path.Combine(featureDir, "Enums");
        Directory.CreateDirectory(enumsDir);

        enumName = Upper(enumName);
        var file = Path.Combine(enumsDir, enumName + ".cs");
        if (File.Exists(file))
        {
            Program.Error($"Enum '{enumName}' already exists for entity '{entity}'.");
            return false;
        }

        var content = $@"namespace {solution}.Core.Features.{plural}.Enums;

public enum {enumName}
{{
}}";
        File.WriteAllText(file, content);
        return true;
    }

    static string Upper(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text[1..];
}
