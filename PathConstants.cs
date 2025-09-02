using System.IO;

public static class PathConstants
{
    public const string Persistence = "Persistence";
    public const string Migrations = "Migrations";
    public static string MigrationsRelativePath => Path.Combine(Persistence, Migrations);
}

