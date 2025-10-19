using System;
using System.Text.RegularExpressions;

namespace DotNetArch.Scaffolding;

public static class PackageVersionResolver
{
    public static string ResolveSharedFrameworkPackageVersion(string? targetFramework)
    {
        var major = ResolveTargetMajor(targetFramework);
        return $"{major}.0.0";
    }

    public static int ResolveTargetMajor(string? targetFramework)
    {
        if (string.IsNullOrWhiteSpace(targetFramework))
            return 8;

        var match = Regex.Match(targetFramework, @"net(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed))
        {
            if (parsed < 8)
                return 8;
            if (parsed > 9)
                return 9;
            return parsed;
        }

        return 8;
    }
}
