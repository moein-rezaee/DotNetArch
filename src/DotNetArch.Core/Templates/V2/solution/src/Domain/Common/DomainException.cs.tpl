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

    /// <summary>An optional text: null or blank becomes null; otherwise it is trimmed and limited like <see cref="RequireText"/>.</summary>
    public static string? OptionalText(string? value, string name, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : RequireText(value, name, maxLength);
}
