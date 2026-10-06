using System.Text;

namespace DotNetArch.Core.Hosting;

/// <summary>Collects output lines; used by MCP results and tests.</summary>
public sealed class BufferedToolOutput : IToolOutput
{
    private readonly StringBuilder _text = new();

    public void Section(string emoji, string title, string? description = null)
    {
        _text.AppendLine($"{emoji} {title}");
        if (!string.IsNullOrWhiteSpace(description))
            _text.AppendLine($"   {description}");
    }

    public void SubStep(bool success, string message) =>
        _text.AppendLine($"   {(success ? "ok" : "fail")}: {message}");

    public void Blank() { }

    public override string ToString() => _text.ToString();
}
