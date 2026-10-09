using DotNetArch.Core.Doctor;

namespace DotNetArch.Core.Operations;

internal static class DoctorOperation
{
    public static readonly OperationDefinition Definition = new(
        "doctor",
        "Diagnose an existing repository against the standard: layers and dependency direction, tests and coverage, build hygiene, configuration and secrets, Docker/CI, documentation, code rules, kit boundaries and the active profile. Read-only; findings carry id, severity, locations and a fix hint. Exit code 3 when blocking.",
        OperationKind.ReadOnly,
        new[]
        {
            new OperationParameter("path", "Repository root (default: current folder).", Positional: true),
            new OperationParameter("profile", "Profile file to apply instead of .net-arch/profile.yml."),
            new OperationParameter("strict", "Treat warnings as blocking.", ParameterType.Flag),
        },
        Run);

    private static OperationResult Run(OperationRequest request)
    {
        var report = DoctorRunner.Run(request.Get("path") ?? Directory.GetCurrentDirectory(), new DoctorOptions(request.Get("profile")));
        var healthy = report.IsHealthy(request.Flag("strict"));
        return new OperationResult(healthy, DoctorFormatter.ToText(report), DoctorFormatter.ToData(report), ExitCode: healthy ? 0 : 3, Error: healthy ? null : $"{report.Errors} blocking error(s)");
    }
}
