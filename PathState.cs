using System;
using System.IO;

public static class PathState
{
    private const string FileName = ".dotnet-arch-state";

    public static void Save(string solutionPath)
    {
        var file = GetFile();
        File.WriteAllText(file, solutionPath);
    }

    public static string? Load()
    {
        var file = GetFile();
        if (!File.Exists(file))
            return null;
        return File.ReadAllText(file).Trim();
    }

    private static string GetFile()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, FileName);
    }
}
