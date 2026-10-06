namespace DotNetArch.Core.Hosting;

/// <summary>Asks the person (or agent) driving the tool for missing input.</summary>
public interface IPrompter
{
    string Ask(string message, string? defaultValue = null);

    bool AskYesNo(string message, bool defaultYes);

    string AskOption(string message, string[] options, int defaultIndex = 0, int[]? disabledIndices = null);
}
