using DotNetArch.Core.Doctor;

namespace DotNetArch.Cli.Commands;

/// <summary>
/// <c>doctor [path] [--profile=auto|generic|corevia] [--json] [--strict] [--out=file]</c>: read-only diagnosis of an existing repository.
/// Exit codes: 0 healthy, 3 blocking findings (errors, or warnings with --strict), 1 usage error.
/// </summary>
internal sealed class DoctorCommand : ICommand
{
    public bool Matches(string[] args) => CommandMatch.Is(args, "doctor");

    public int Run(string[] args)
    {
        var parsed = CommandArgs.Parse(args, 1);
        var profileName = parsed.Get("profile") ?? "auto";
        if (!Enum.TryParse<DoctorProfile>(profileName, ignoreCase: true, out var profile))
        {
            System.Console.Error.WriteLine($"Unknown profile '{profileName}'. Use auto, generic or corevia.");
            return 1;
        }

        var root = parsed.Positionals.FirstOrDefault() ?? parsed.Get("output") ?? Directory.GetCurrentDirectory();
        var report = DoctorRunner.Run(root, new DoctorOptions(profile));
        var text = parsed.Has("json") ? DoctorFormatter.ToJson(report) : DoctorFormatter.ToText(report);
        if (parsed.Get("out") is { Length: > 0 } outFile)
            File.WriteAllText(outFile, text);
        else
            System.Console.Out.Write(text);
        return report.IsHealthy(parsed.Has("strict")) ? 0 : 3;
    }
}
