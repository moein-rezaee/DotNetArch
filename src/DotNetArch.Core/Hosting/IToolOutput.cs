namespace DotNetArch.Core.Hosting;

/// <summary>Human-readable progress output. Implementations decide where it goes (console, stderr, buffer).</summary>
public interface IToolOutput
{
    void Section(string emoji, string title, string? description = null);

    void SubStep(bool success, string message);

    void Blank();
}
