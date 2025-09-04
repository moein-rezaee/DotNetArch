using System;
public static class Logger
{
    static bool _firstLine = true;
    private static void Write(string prefix, string title, string? description = null, bool indent = false)
    {
        var padding = indent ? "   " : string.Empty;
        if (!indent && !_firstLine)
            Console.WriteLine();
        Console.WriteLine($"{padding}{prefix}  {title}");
        if (!string.IsNullOrWhiteSpace(description))
            Console.WriteLine($"{padding}   {description}");
        if (!indent)
            _firstLine = false;
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
