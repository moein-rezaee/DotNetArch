using System;
public static class Logger
{
    private static void Write(string prefix, string title, string? description = null, bool indent = false)
    {
        var padding = indent ? "   " : string.Empty;
        Console.WriteLine($"{padding}{prefix} {title}");
        if (!string.IsNullOrWhiteSpace(description))
            Console.WriteLine($"{padding}   {description}");
    }

    public static void Section(string emoji, string title, string? description = null)
        => Write($"{emoji}", title, description);

    public static void SubStep(bool success, string message)
        => Write(success ? "✅" : "❌", message, null, indent: true);

    public static void Info(string title, string? description = null)
        => Section("ℹ️", title, description);

    public static void Success(string title, string? description = null)
        => Section("✅", title, description);

    public static void Error(string title, string? description = null)
        => Section("❌", title, description);

    public static void Blank() => Console.WriteLine();
}
