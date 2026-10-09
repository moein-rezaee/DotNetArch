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

    /// <summary>CI provider id (github, gitlab, azure, bitbucket) or empty when none was generated.</summary>
    public string CiProvider { get; set; } = "";

    /// <summary>Base URL of a personal/self-hosted git server (empty for the public hosts).</summary>
    public string GitHost { get; set; } = "";

    /// <summary>Git server flavour: github, gitlab, gitea, azure, bitbucket.</summary>
    public string GitProvider { get; set; } = "";

    /// <summary>Private container registry host (e.g. registry.example.com/team); empty for local images only.</summary>
    public string DockerRegistry { get; set; } = "";

    /// <summary>Private NuGet feed URL added next to nuget.org; credentials are never stored.</summary>
    public string NuGetSource { get; set; } = "";

    public string NuGetSourceName { get; set; } = "";

    /// <summary>Package id prefix for generated kits (<c>&lt;Prefix&gt;.Kit.&lt;Area&gt;.*</c>); defaults to the solution name.</summary>
    public string KitPrefix { get; set; } = "";

    /// <summary>Kits wired into this solution: area name to its selected providers.</summary>
    public Dictionary<string, string> Kits { get; set; } = new();

    /// <summary>True when the solution has an MCP host (<c>src/&lt;App&gt;.Mcp</c>); new entities then get MCP tools too.</summary>
    public bool McpEnabled { get; set; }

    public const string LegacyLayout = "legacy";
    public const string V2Layout = "v2";

    /// <summary>ABP-aligned layout (D-26): the v2 tree with a singular <c>test/</c> folder and the ABP layer projects.</summary>
    public const string V3Layout = "v3";

    /// <summary>True for the layouts that keep projects under <c>src/</c> (v2 and v3).</summary>
    public bool IsV2 => Layout.Equals(V2Layout, StringComparison.OrdinalIgnoreCase) || IsV3;

    public bool IsV3 => Layout.Equals(V3Layout, StringComparison.OrdinalIgnoreCase);

    /// <summary>Solution-relative folder of the test projects: <c>tests</c> (v2) or <c>test</c> (v3).</summary>
    public string TestsRoot => IsV3 ? "test" : "tests";

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
