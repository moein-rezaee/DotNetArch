namespace {{Prefix}}.Kit.{{Area}}.Abstractions;

/// <summary>Base type of every failure raised by the {{Area}} kit. <see cref="ErrorCode"/> is stable and safe to map to API errors.</summary>
public class {{Area}}Exception(string message, string errorCode = "{{AreaSnake}}_error", Exception? innerException = null)
    : Exception(message, innerException)
{
    public string ErrorCode { get; } = errorCode;
}
