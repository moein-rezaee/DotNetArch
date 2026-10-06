namespace {{App}}.Api.Endpoints;

/// <summary>A minimal-API endpoint (group). Implementations are discovered by assembly scan and mapped by <c>MapEndpoints()</c>.</summary>
public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}
