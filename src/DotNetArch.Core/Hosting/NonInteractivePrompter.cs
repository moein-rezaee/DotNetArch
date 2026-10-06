namespace DotNetArch.Core.Hosting;

/// <summary>Prompter for hosts without a person (MCP, CI, tests): defaults win, free text without default fails loudly.</summary>
public sealed class NonInteractivePrompter : IPrompter
{
    public string Ask(string message, string? defaultValue = null) =>
        defaultValue ?? throw new MissingInputException(message);

    public bool AskYesNo(string message, bool defaultYes) => defaultYes;

    public string AskOption(string message, string[] options, int defaultIndex = 0, int[]? disabledIndices = null)
    {
        var disabled = disabledIndices is null ? new HashSet<int>() : new HashSet<int>(disabledIndices);
        var index = Math.Clamp(defaultIndex, 0, options.Length - 1);
        if (disabled.Contains(index))
            index = Enumerable.Range(0, options.Length).First(i => !disabled.Contains(i));
        return options[index];
    }
}
