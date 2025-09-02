using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using DotNetArch.Scaffolding;

namespace DotNetArch.Scaffolding.Steps;

public class MinimalApiStep : IScaffoldStep
{
    public void Execute(SolutionConfig config, string entity)
    {
        var solution = config.SolutionName;
        var basePath = config.SolutionPath;
        var startupProject = config.StartupProject;
        var plural = Naming.Pluralize(entity);
        var apiDir = Path.Combine(basePath, startupProject, "Features", plural);
        Directory.CreateDirectory(apiDir);
        var file = Path.Combine(apiDir, $"{entity}Endpoints.cs");
        if (!File.Exists(file))
        {
            var content = """
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using {{solution}}.Application.Features.{{entities}}.Commands.Create;
using {{solution}}.Application.Features.{{entities}}.Commands.Update;
using {{solution}}.Application.Features.{{entities}}.Commands.Delete;
using {{solution}}.Application.Features.{{entities}}.Queries.GetById;
using {{solution}}.Application.Features.{{entities}}.Queries.GetAll;
using {{solution}}.Application.Features.{{entities}}.Queries.GetList;
using {{solution}}.Core.Features.{{entities}}.Entities;
using {{solution}}.Core.Common.Models;

namespace {{startupProject}}.Features.{{entities}};

public static class {{entity}}Endpoints
{
    public static void Map{{entity}}Endpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/Api/{{entity}}/{id}", async (IMediator mediator, int id) =>
            await mediator.Send(new Get{{entity}}ByIdQuery(id)) is {{entity}} result ? Results.Ok(result) : Results.NotFound())
            .WithTags("{{entity}}");

        routes.MapGet("/Api/{{entity}}/All", async (IMediator mediator) =>
            Results.Ok(await mediator.Send(new Get{{entity}}AllQuery())))
            .WithTags("{{entity}}");

        routes.MapGet("/Api/{{entity}}/List", async (IMediator mediator, int page, int pageSize) =>
            Results.Ok(await mediator.Send(new Get{{entity}}ListQuery(page, pageSize))))
            .WithTags("{{entity}}");

        routes.MapPost("/Api/{{entity}}", async (IMediator mediator, {{entity}} entity) =>
        {
            var created = await mediator.Send(new Create{{entity}}Command(entity));
            return Results.Created($"/Api/{{entity}}/{created.Id}", created);
        }).WithTags("{{entity}}");

        routes.MapPut("/Api/{{entity}}/{id}", async (IMediator mediator, int id, {{entity}} entity) =>
        {
            entity.Id = id;
            await mediator.Send(new Update{{entity}}Command(entity));
            return Results.NoContent();
        }).WithTags("{{entity}}");

        routes.MapDelete("/Api/{{entity}}/{id}", async (IMediator mediator, int id) =>
        {
            await mediator.Send(new Delete{{entity}}Command(id));
            return Results.NoContent();
        }).WithTags("{{entity}}");
    }
}
""";
            File.WriteAllText(file, content
                .Replace("{{solution}}", solution)
                .Replace("{{entity}}", entity)
                .Replace("{{entities}}", plural)
                .Replace("{{startupProject}}", startupProject));
            return;
        }

        var lines = File.ReadAllLines(file).ToList();
        var lastUsing = lines.FindLastIndex(l => l.StartsWith("using "));
        void EnsureUsing(string ns)
        {
            var statement = $"using {ns};";
            if (!lines.Any(l => l.Trim() == statement))
                lines.Insert(++lastUsing, statement);
        }

        EnsureUsing($"{solution}.Application.Features.{plural}.Commands.Create");
        EnsureUsing($"{solution}.Application.Features.{plural}.Commands.Update");
        EnsureUsing($"{solution}.Application.Features.{plural}.Commands.Delete");
        EnsureUsing($"{solution}.Application.Features.{plural}.Queries.GetById");
        EnsureUsing($"{solution}.Application.Features.{plural}.Queries.GetAll");
        EnsureUsing($"{solution}.Application.Features.{plural}.Queries.GetList");
        EnsureUsing($"{solution}.Core.Common.Models");

        if (!lines.Any(l => l.Contains($"Get{entity}ByIdQuery")))
        {
            var classClose = lines.FindLastIndex(l => l.Trim() == "}");
            var methodClose = lines.FindLastIndex(classClose - 1, l => l.Trim() == "}");
            var insertIndex = methodClose < 0 ? classClose : methodClose;
            var crudLines = new[]
            {
                $"        routes.MapGet(\"/Api/{entity}/{{id}}\", async (IMediator mediator, int id) =>",
                $"            await mediator.Send(new Get{entity}ByIdQuery(id)) is {entity} result ? Results.Ok(result) : Results.NotFound())",
                $"            .WithTags(\"{entity}\");",
                "",
                $"        routes.MapGet(\"/Api/{entity}/All\", async (IMediator mediator) =>",
                $"            Results.Ok(await mediator.Send(new Get{entity}AllQuery())))",
                $"            .WithTags(\"{entity}\");",
                "",
                $"        routes.MapGet(\"/Api/{entity}/List\", async (IMediator mediator, int page, int pageSize) =>",
                $"            Results.Ok(await mediator.Send(new Get{entity}ListQuery(page, pageSize))))",
                $"            .WithTags(\"{entity}\");",
                "",
                $"        routes.MapPost(\"/Api/{entity}\", async (IMediator mediator, {entity} entity) =>",
                "        {",
                $"            var created = await mediator.Send(new Create{entity}Command(entity));",
                $"            return Results.Created($\"/Api/{entity}/{{created.Id}}\", created);",
                $"        }}).WithTags(\"{entity}\");",
                "",
                $"        routes.MapPut(\"/Api/{entity}/{{id}}\", async (IMediator mediator, int id, {entity} entity) =>",
                "        {",
                $"            entity.Id = id;",
                $"            await mediator.Send(new Update{entity}Command(entity));",
                $"            return Results.NoContent();",
                $"        }}).WithTags(\"{entity}\");",
                "",
                $"        routes.MapDelete(\"/Api/{entity}/{{id}}\", async (IMediator mediator, int id) =>",
                "        {",
                $"            await mediator.Send(new Delete{entity}Command(id));",
                $"            return Results.NoContent();",
                $"        }}).WithTags(\"{entity}\");",
            };
            lines.InsertRange(insertIndex, crudLines);
        }

        File.WriteAllLines(file, lines);
    }
}
