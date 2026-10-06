using System.Runtime.InteropServices;

namespace DotNetArch.Core.Scaffolding.Solution;

/// <summary>Detection and installation of the external tools the generator depends on (dotnet SDK, git, dotnet-ef).</summary>
public static class SolutionTooling
{
    public static bool EnsureDotnetSdk()
    {
        if (ToolHost.RunCommand("dotnet --version", print: false))
            return true;

        ToolHost.Error(".NET SDK not found. Install from https://dotnet.microsoft.com/download");
        return false;
    }

    public static bool IsGitInstalled() => ToolHost.RunCommand("git --version", print: false);

    public static bool EnsureEfTool(string? workingDir = null)
    {
        if (ToolHost.RunCommand("dotnet ef --version", workingDir, print: false))
            return true;

        ToolHost.Info("dotnet-ef not found. Attempting installation...");
        var install = GetEfToolInstallCommand(workingDir);
        if (ToolHost.RunCommand(install, workingDir))
        {
            AddDotnetToolsToPath();
            return ToolHost.RunCommand("dotnet ef --version", workingDir, print: false);
        }

        ToolHost.Error($"Failed to install dotnet-ef. Install manually with: {install}");
        return false;
    }

    public static string GetEfToolInstallCommand(string? workingDir = null)
    {
        var version = "8.*";
        try
        {
            if (!string.IsNullOrWhiteSpace(workingDir))
            {
                var cfg = ConfigManager.Load(workingDir!);
                if (cfg != null && !string.IsNullOrWhiteSpace(cfg.TargetFramework) && cfg.TargetFramework.StartsWith("net9."))
                    version = "9.*";
            }
        }
        catch (IOException) { }

        return $"dotnet tool install --global dotnet-ef --version {version}";
    }

    /// <summary>Makes a freshly installed global tool visible to child processes of this run.</summary>
    private static void AddDotnetToolsToPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var tools = Path.Combine(home, ".dotnet", "tools");
        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (!path.Split(separator).Contains(tools, StringComparer.Ordinal))
            Environment.SetEnvironmentVariable("PATH", path + separator + tools);
    }

    /// <summary>Highest installed SDK major (minimum 8) as a target framework moniker.</summary>
    public static string ResolveTargetFramework()
    {
        try
        {
            var (ok, output) = ToolHost.RunCommandCapture("dotnet --list-sdks");
            if (!ok) return "net8.0";
            var versions = new List<Version>();
            foreach (var raw in output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (token == null) continue;
                var verStr = token.Split('-')[0];
                if (Version.TryParse(verStr, out var v)) versions.Add(v);
            }
            if (versions.Count == 0) return "net8.0";
            var major = Math.Max(8, versions.Max().Major);
            return $"net{major}.0";
        }
        catch (InvalidOperationException) { return "net8.0"; }
    }
}
