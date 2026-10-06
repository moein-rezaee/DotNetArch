namespace {{App}}.Domain.Common;

/// <summary>A business rule was violated. Mapped to a 400 problem response at the API boundary.</summary>
public class DomainException(string message) : Exception(message)
{
    public static string RequireText(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{name} is required.");

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainException($"{name} must be at most {maxLength} characters.");

        return trimmed;
    }
}
