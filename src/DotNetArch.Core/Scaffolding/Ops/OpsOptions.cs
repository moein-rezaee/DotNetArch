namespace DotNetArch.Core.Scaffolding.Ops;

/// <summary>Operational options for a new solution: container, git host, CI and private registries. Null/empty means "decide for me".</summary>
public sealed record OpsOptions(
    string? Ci = null,
    string? GitRemote = null,
    string? GitHost = null,
    string? GitProvider = null,
    string? DockerRegistry = null,
    string? NuGetSource = null,
    string? NuGetSourceName = null,
    bool NoDocker = false,
    bool NoGit = false);
