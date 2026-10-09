using System.Runtime.CompilerServices;

namespace DotNetArch.Core.Tests;

internal static class TestEnvironment
{
    /// <summary>Tests must never read the developer's real ~/.net-arch; point the global folder at an empty temp folder.</summary>
    [ModuleInitializer]
    internal static void IsolateGlobalSettings()
    {
        var folder = Path.Combine(Path.GetTempPath(), "dotnet-arch-global-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        Environment.SetEnvironmentVariable("DOTNET_ARCH_HOME", folder);
    }
}
