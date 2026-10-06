using DotNetArch.Core.Hosting;

namespace DotNetArch.Cli.Console;

/// <summary>Writes tool progress to the terminal with the legacy emoji layout.</summary>
public sealed class ConsoleToolOutput : IToolOutput
{
    private bool _firstLine = true;

    public void Section(string emoji, string title, string? description = null) =>
        Write(emoji, title, description, indent: false);

    public void SubStep(bool success, string message) =>
        Write(success ? "✅" : "❌", message, null, indent: true);

    public void Blank() => System.Console.WriteLine();

    private void Write(string prefix, string title, string? description, bool indent)
    {
        var padding = indent ? "   " : string.Empty;
        if (!indent && !_firstLine)
            System.Console.WriteLine();
        System.Console.WriteLine($"{padding}{prefix}  {title}");
        if (!string.IsNullOrWhiteSpace(description))
            System.Console.WriteLine($"{padding}   {description}");
        if (!indent)
            _firstLine = false;
    }
}
