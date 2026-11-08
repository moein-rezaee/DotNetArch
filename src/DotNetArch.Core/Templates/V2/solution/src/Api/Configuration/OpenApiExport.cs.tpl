using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Swashbuckle.AspNetCore.Swagger;

namespace {{App}}.Api.Configuration;

/// <summary>
/// <c>--export-openapi [file]</c> writes the service's OpenAPI document (default <c>docs/api/openapi.json</c>) and exits without starting
/// the server or touching the database. The service registration (Corevia Morph module registry) and the doctor rule DA-M09 use this file.
/// </summary>
public static class OpenApiExport
{
    public const string Flag = "--export-openapi";
    private const string DefaultPath = "docs/api/openapi.json";

    public static bool TryGetPath(string[] args, out string path)
    {
        var index = Array.IndexOf(args, Flag);
        path = index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal) ? args[index + 1] : DefaultPath;
        return index >= 0;
    }

    public static async Task WriteAsync(IServiceProvider services, string path, CancellationToken cancellationToken = default)
    {
        var document = services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, document.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0), cancellationToken);
    }
}
