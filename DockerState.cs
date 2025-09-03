using System;
using System.IO;
using System.Text.Json;

public class DockerStateData
{
    public string? LastContainer { get; set; }
    public string? LastImage { get; set; }
}

public static class DockerState
{
    private const string FileName = ".dotnet-arch-docker";

    public static void Save(DockerStateData data)
    {
        var file = GetFile();
        var json = JsonSerializer.Serialize(data);
        File.WriteAllText(file, json);
    }

    public static DockerStateData Load()
    {
        var file = GetFile();
        if (!File.Exists(file))
            return new DockerStateData();
        var json = File.ReadAllText(file);
        return JsonSerializer.Deserialize<DockerStateData>(json) ?? new DockerStateData();
    }

    private static string GetFile()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, FileName);
    }
}
