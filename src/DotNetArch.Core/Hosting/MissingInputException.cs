namespace DotNetArch.Core.Hosting;

/// <summary>Thrown by non-interactive hosts when a value is required but was not supplied.</summary>
public sealed class MissingInputException(string prompt) : Exception($"Missing required input: {prompt}")
{
    public string Prompt { get; } = prompt;
}
