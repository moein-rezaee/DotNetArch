using System;
using System.Collections.Generic;

namespace DotNetArch.Core.Config;

public class SolutionConfig
{
    public string SolutionName { get; set; } = "";
    public string SolutionPath { get; set; } = "";
    public string StartupProject { get; set; } = "";
    public string DatabaseProvider { get; set; } = "";
    public string ApiStyle { get; set; } = "controller";
    public string ApiPort { get; set; } = "";
    public string DockerImage { get; set; } = "";
    public string DockerContainer { get; set; } = "";
    public string TargetFramework { get; set; } = "net8.0";

    /// <summary>Folder layout: <see cref="LegacyLayout"/> (flat, default when the key is absent) or <see cref="V2Layout"/> (src/ + tests/).</summary>
    public string Layout { get; set; } = LegacyLayout;

    public const string LegacyLayout = "legacy";
    public const string V2Layout = "v2";

    public bool IsV2 => Layout.Equals(V2Layout, StringComparison.OrdinalIgnoreCase);

    /// <summary>Solution-relative path of a project's csproj for the active layout.</summary>
    public string ProjectFile(string projectName) =>
        IsV2 ? $"src/{projectName}/{projectName}.csproj" : $"{projectName}/{projectName}.csproj";

    /// <summary>Solution-relative path of a project's folder for the active layout.</summary>
    public string ProjectDirectory(string projectName) => IsV2 ? $"src/{projectName}" : projectName;

    public Dictionary<string, EntityStatus> Entities { get; set; } = new();
}

public class EntityStatus
{
    public bool HasCrud { get; set; }
    public bool HasAction { get; set; }
}
