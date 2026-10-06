using System;
using System.IO;
using DotNetArch.Core.Scaffolding;

namespace DotNetArch.Core.Scaffolding.Entities;

public static class EnumScaffolder
{
    public static bool EntityExists(SolutionConfig config, string entity)
    {
        if (config.IsV2)
            return V2.EntityGeneration.EntityExists(config, entity);

        var solution = config.SolutionName;
        var plural = Naming.Pluralize(entity);
        var featureDir = Path.Combine(config.SolutionPath, $"{solution}.Core", "Features", plural);
        return Directory.Exists(featureDir);
    }

    public static bool Generate(SolutionConfig config, string? entity, string enumName)
    {
        if (config.IsV2)
            return V2.EntityGeneration.GenerateEnum(config, entity, enumName);

        Identifier.RequireIfPresent(entity, "entity name");
        Identifier.RequireIfPresent(enumName, "enum name");
        if (string.IsNullOrWhiteSpace(config.SolutionName) ||
            string.IsNullOrWhiteSpace(enumName))
        {
            ToolHost.Error("Solution and enum names are required.");
            return false;
        }

        var solution = config.SolutionName;
        string enumsDir;
        string @namespace;

        if (!string.IsNullOrWhiteSpace(entity))
        {
            if (!EntityExists(config, entity))
            {
                ToolHost.Error($"Entity '{entity}' does not exist.");
                return false;
            }

            var plural = Naming.Pluralize(entity);
            var coreDir = Path.Combine(config.SolutionPath, $"{solution}.Core");
            var featureDir = Path.Combine(coreDir, "Features", plural);
            enumsDir = Path.Combine(featureDir, "Enums");
            @namespace = $"{solution}.Core.Features.{plural}.Enums";
        }
        else
        {
            var coreDir = Path.Combine(config.SolutionPath, $"{solution}.Core");
            enumsDir = Path.Combine(coreDir, "Common", "Enums");
            @namespace = $"{solution}.Core.Common.Enums";
        }

        Directory.CreateDirectory(enumsDir);

        enumName = Upper(enumName);
        var file = Path.Combine(enumsDir, enumName + ".cs");
        if (File.Exists(file))
        {
            var msg = string.IsNullOrWhiteSpace(entity)
                ? $"Enum '{enumName}' already exists."
                : $"Enum '{enumName}' already exists for entity '{entity}'.";
            ToolHost.Error(msg);
            return false;
        }

        var content = $@"namespace {@namespace};

public enum {enumName}
{{
}}";
        File.WriteAllText(file, content);
        return true;
    }

    static string Upper(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text[1..];
}
