using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetArch.Core.Doctor;

public static class DoctorFormatter
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string ToJson(DoctorReport report) => JsonSerializer.Serialize(ToData(report), Json);

    public static object ToData(DoctorReport report) =>
        new
        {
            report.Root,
            report.Profile,
            report.Layout,
            summary = new { report.ChecksRun, passed = report.Passed.Count, report.Errors, report.Warnings, report.Infos },
            report.Projects,
            report.Passed,
            report.Findings,
            report.Accepted,
            report.Notes,
        };

    public static string ToText(DoctorReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"dotnet-arch doctor - {report.Root}");
        sb.AppendLine($"profile: {report.Profile}   layout: {report.Layout}   projects: {report.Projects.Count}");
        sb.AppendLine($"checks: {report.ChecksRun}   passed: {report.Passed.Count}   errors: {report.Errors}   warnings: {report.Warnings}   info: {report.Infos}");
        foreach (var note in report.Notes)
            sb.AppendLine($"note: {note}");
        foreach (var group in report.Findings.GroupBy(f => f.Category))
        {
            sb.AppendLine();
            sb.AppendLine($"[{group.Key}]");
            foreach (var f in group)
            {
                sb.AppendLine($"  {Mark(f.Severity)} {f.Id}  {f.Message}");
                if (f.Path != null)
                    sb.AppendLine($"        at: {f.Path}");
                if (f.Details != null)
                    foreach (var d in f.Details)
                        sb.AppendLine($"        - {d}");
                if (f.Hint != null)
                    sb.AppendLine($"        fix: {f.Hint}");
            }
        }

        if (report.Accepted.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("[accepted exceptions]");
            foreach (var a in report.Accepted)
                sb.AppendLine($"  ok    {a.Id}  {a.Message} - {a.Reason}");
        }

        sb.AppendLine();
        sb.AppendLine(report.Errors == 0 ? "result: OK (no blocking errors)" : $"result: {report.Errors} blocking error(s)");
        return sb.ToString();
    }

    private static string Mark(DoctorSeverity severity) => severity switch
    {
        DoctorSeverity.Error => "ERROR",
        DoctorSeverity.Warning => "WARN ",
        _ => "info ",
    };
}
