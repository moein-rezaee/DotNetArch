using {{App}}.Application.Common.Exceptions;
using {{App}}.Domain.Common;
using FluentValidation;
using ModelContextProtocol;

namespace {{App}}.Mcp.Tools;

/// <summary>Runs a tool operation and turns expected failures into MCP errors with a readable message (anything else stays hidden).</summary>
internal static class ToolRunner
{
    public static async Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (ValidationException exception)
        {
            throw new McpException("Validation failed: " + string.Join("; ", exception.Errors.Select(error => $"{error.PropertyName}: {error.ErrorMessage}")));
        }
        catch (NotFoundException exception)
        {
            throw new McpException(exception.Message);
        }
        catch (DomainException exception)
        {
            throw new McpException(exception.Message);
        }
    }

    public static async Task<string> RunAsync(Func<Task> operation, string successMessage)
    {
        await RunAsync(async () =>
        {
            await operation();
            return true;
        });
        return successMessage;
    }
}
