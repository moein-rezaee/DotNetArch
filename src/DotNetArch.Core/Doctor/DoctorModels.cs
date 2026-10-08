namespace DotNetArch.Core.Doctor;

public enum DoctorSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>Rule set a repository is judged by. <c>Generic</c> is the public DotNetArch standard; <c>Corevia</c> adds the Corevia governance rules.</summary>
public enum DoctorProfile
{
    Auto,
    Generic,
    Corevia,
}

/// <summary>One thing the doctor found. <see cref="Details"/> lists the concrete locations (capped) so an agent can act without re-scanning.</summary>
public sealed record DoctorFinding(
    string Id,
    DoctorSeverity Severity,
    string Category,
    string Message,
    string? Path = null,
    string? Hint = null,
    IReadOnlyList<string>? Details = null);

public sealed record DoctorOptions(DoctorProfile Profile = DoctorProfile.Auto);

public sealed record DoctorReport(
    string Root,
    string Profile,
    string Layout,
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> Passed,
    IReadOnlyList<DoctorFinding> Findings)
{
    public int Errors => Findings.Count(f => f.Severity == DoctorSeverity.Error);

    public int Warnings => Findings.Count(f => f.Severity == DoctorSeverity.Warning);

    public int Infos => Findings.Count(f => f.Severity == DoctorSeverity.Info);

    public int ChecksRun => Passed.Count + Findings.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count();

    /// <summary>True when nothing blocks adoption: no errors, and (when <paramref name="strict"/>) no warnings either.</summary>
    public bool IsHealthy(bool strict = false) => Errors == 0 && (!strict || Warnings == 0);
}
