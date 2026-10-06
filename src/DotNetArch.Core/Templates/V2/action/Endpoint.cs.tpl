using {{App}}.Api.Endpoints;
using {{App}}.Application.Features.{{Plural}}.Actions.{{ActionName}}{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Dtos;
using MediatR;

namespace {{App}}.Api.Endpoints.{{Plural}};

internal sealed class {{ActionName}}{{Entity}}Endpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.Map{{HttpVerb}}("api/{{RouteName}}/{id:guid}/{{ActionRoute}}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new {{ActionName}}{{Entity}}{{RequestKind}}(id), cancellationToken)))
            .WithTags("{{Plural}}")
            .Produces<{{Entity}}Dto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
}
