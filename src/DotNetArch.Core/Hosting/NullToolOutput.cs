namespace DotNetArch.Core.Hosting;

public sealed class NullToolOutput : IToolOutput
{
    public void Section(string emoji, string title, string? description = null) { }

    public void SubStep(bool success, string message) { }

    public void Blank() { }
}
