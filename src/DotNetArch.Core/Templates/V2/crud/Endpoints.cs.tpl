using {{App}}.Api.Endpoints;
using {{App}}.Application.Common.Pagination;
using {{App}}.Application.Features.{{Plural}}.Commands.Create{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Commands.Delete{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Commands.Update{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Dtos;
using {{App}}.Application.Features.{{Plural}}.Queries.Get{{Entity}}ById;
using {{App}}.Application.Features.{{Plural}}.Queries.Get{{Plural}};
using MediatR;

namespace {{App}}.Api.Endpoints.{{Plural}};

internal sealed class {{Plural}}Endpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/{{RouteName}}").WithTags("{{Plural}}");

        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken, int pageNumber = 1, int pageSize = 20) =>
            Results.Ok(await sender.Send(new Get{{Plural}}Query(pageNumber, pageSize), cancellationToken)))
            .Produces<PagedResult<{{Entity}}Dto>>();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new Get{{Entity}}ByIdQuery(id), cancellationToken)))
            .WithName("Get{{Entity}}ById")
            .Produces<{{Entity}}Dto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (Create{{Entity}}Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            var created = await sender.Send(new Create{{Entity}}Command(request.Name), cancellationToken);
            return Results.CreatedAtRoute("Get{{Entity}}ById", new { id = created.Id }, created);
        })
            .Produces<{{Entity}}Dto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", async (Guid id, Update{{Entity}}Request request, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new Update{{Entity}}Command(id, request.Name), cancellationToken)))
            .Produces<{{Entity}}Dto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new Delete{{Entity}}Command(id), cancellationToken);
            return Results.NoContent();
        })
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
