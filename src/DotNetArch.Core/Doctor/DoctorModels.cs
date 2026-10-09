namespace DotNetArch.Core.Doctor;

public enum DoctorSeverity
{
    Info,
    Warning,
    Error,
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

/// <summary>A finding that an exception from rules.yml accepted; it no longer counts but stays visible.</summary>
public sealed record AcceptedFinding(string Id, string Message, string Reason);

/// <param name="ProfilePath">Explicit profile file; when null the project's <c>.net-arch/profile.yml</c> is used, if present.</param>
public sealed record DoctorOptions(string? ProfilePath = null);

public sealed record DoctorReport(
    string Root,
    string Profile,
    string Layout,
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> Passed,
    IReadOnlyList<DoctorFinding> Findings,
    IReadOnlyList<AcceptedFinding> Accepted,
    IReadOnlyList<string> Notes)
{
    public int Errors => Findings.Count(f => f.Severity == DoctorSeverity.Error);

    public int Warnings => Findings.Count(f => f.Severity == DoctorSeverity.Warning);

    public int Infos => Findings.Count(f => f.Severity == DoctorSeverity.Info);

    public int ChecksRun => Passed.Count + Findings.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count() + Accepted.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count();

    /// <summary>True when nothing blocks adoption: no errors, and (when <paramref name="strict"/>) no warnings either.</summary>
    public bool IsHealthy(bool strict = false) => Errors == 0 && (!strict || Warnings == 0);
}
