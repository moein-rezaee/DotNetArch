using System;
using System.IO;
using DotNetArch.Scaffolding;

namespace DotNetArch.Scaffolding.Steps;

public class ApplicationStep : IScaffoldStep
{
    public void Execute(SolutionConfig config, string entity)
    {
        var solution = config.SolutionName;
        var basePath = config.SolutionPath;
        var plural = Naming.Pluralize(entity);
        var noDb = string.Equals(config.DatabaseProvider, "None", StringComparison.OrdinalIgnoreCase);
        var appRoot = Path.Combine(basePath, $"{solution}.Application");
        Directory.CreateDirectory(appRoot);
        var marker = Path.Combine(appRoot, "AssemblyMarker.cs");
        if (!File.Exists(marker))
        {
            var markerContent = $"namespace {solution}.Application;{Environment.NewLine}{Environment.NewLine}public class AssemblyMarker {{ }}{Environment.NewLine}";
            File.WriteAllText(marker, markerContent);
        }
        if (string.IsNullOrWhiteSpace(entity))
            return;
        var appBase = Path.Combine(appRoot, "Features", plural);
        Directory.CreateDirectory(appBase);

        var commandsDir = Path.Combine(appBase, "Commands");
        var queriesDir = Path.Combine(appBase, "Queries");
        var modelsDir = Path.Combine(appBase, "Models");
        Directory.CreateDirectory(commandsDir);
        Directory.CreateDirectory(queriesDir);
        Directory.CreateDirectory(modelsDir);

        // View Models (presentation models) under Application per-entity
        var modelPath = Path.Combine(modelsDir, $"{entity}Model.cs");
        if (!File.Exists(modelPath))
        {
            File.WriteAllText(modelPath, Fill(@"
using {{solution}}.Core.Features.{{entities}}.Entities;

namespace {{solution}}.Application.Features.{{entities}}.Models;

public class {{entity}}Model
{
    public int Id { get; set; }

    // Casting between Entity and Model
    public static implicit operator {{entity}}Model?({{entity}}? e)
        => e is null
            ? null
            : new {{entity}}Model { Id = e.Id };

    public static explicit operator {{entity}}({{entity}}Model m)
        => new {{entity}} { Id = m.Id };
}
"));
        }

        string Fill(string template) => template
            .Replace("{{solution}}", solution)
            .Replace("{{entity}}", entity)
            .Replace("{{entities}}", plural);

        // Commands
        var createDir = Path.Combine(commandsDir, "Create");
        Directory.CreateDirectory(createDir);
        // Commands: take needed properties directly (no DTO)
        var createCmdPath = Path.Combine(createDir, $"Create{entity}Command.cs");
        if (!File.Exists(createCmdPath))
            File.WriteAllText(createCmdPath, Fill(noDb ? @"
using MediatR;

namespace {{solution}}.Application.Features.{{entities}}.Commands.Create;

public record Create{{entity}}Command() : IRequest;
" : @"
using MediatR;
using {{solution}}.Core.Features.{{entities}}.Entities;

namespace {{solution}}.Application.Features.{{entities}}.Commands.Create;

public record Create{{entity}}Command() : IRequest<{{entity}}>;
"));
        var createHandlerPath = Path.Combine(createDir, $"Create{entity}Handler.cs");
        if (!File.Exists(createHandlerPath))
            File.WriteAllText(createHandlerPath, Fill(noDb ? @"
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace {{solution}}.Application.Features.{{entities}}.Commands.Create;

public class Create{{entity}}Handler : IRequestHandler<Create{{entity}}Command>
{
    public async Task Handle(Create{{entity}}Command request, CancellationToken ct)
        => await Task.CompletedTask;
}
" : @"
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Features.{{entities}}.Entities;
using {{solution}}.Application.Common.Interfaces;

namespace {{solution}}.Application.Features.{{entities}}.Commands.Create;

public class Create{{entity}}Handler : IRequestHandler<Create{{entity}}Command, {{entity}}>
{
    private readonly IUnitOfWork _uow;
    public Create{{entity}}Handler(IUnitOfWork uow) => _uow = uow;
    public async Task<{{entity}}> Handle(Create{{entity}}Command request, CancellationToken ct)
    {
        var entity = new {{entity}}();
        await _uow.{{entity}}Repository.AddAsync(entity);
        await _uow.SaveChangesAsync();
        return entity;
    }
}
"));
        var createValidatorPath = Path.Combine(createDir, $"Create{entity}Validator.cs");
        if (!File.Exists(createValidatorPath))
            File.WriteAllText(createValidatorPath, Fill(noDb ? @"
using FluentValidation;

namespace {{solution}}.Application.Features.{{entities}}.Commands.Create;

public class Create{{entity}}Validator : AbstractValidator<Create{{entity}}Command>
{
    public Create{{entity}}Validator()
    {
        // No payload in no-db mode; add rules if fields are added later
    }
}
" : @"
using FluentValidation;

namespace {{solution}}.Application.Features.{{entities}}.Commands.Create;

public class Create{{entity}}Validator : AbstractValidator<Create{{entity}}Command>
{
    public Create{{entity}}Validator()
    {
        // No payload by default
    }
}
"));

        var updateDir = Path.Combine(commandsDir, "Update");
        Directory.CreateDirectory(updateDir);
        var updateCmdPath = Path.Combine(updateDir, $"Update{entity}Command.cs");
        if (!File.Exists(updateCmdPath))
            File.WriteAllText(updateCmdPath, Fill(@"
using MediatR;

namespace {{solution}}.Application.Features.{{entities}}.Commands.Update;

public record Update{{entity}}Command(int Id) : IRequest;
"));
        var updateHandlerPath = Path.Combine(updateDir, $"Update{entity}Handler.cs");
        if (!File.Exists(updateHandlerPath))
            File.WriteAllText(updateHandlerPath, Fill(noDb ? @"
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Features.{{entities}}.Entities;

namespace {{solution}}.Application.Features.{{entities}}.Commands.Update;

public class Update{{entity}}Handler : IRequestHandler<Update{{entity}}Command>
{
    public async Task Handle(Update{{entity}}Command request, CancellationToken ct)
    {
        // No database configured. Implement update logic here if needed.
        await Task.CompletedTask;
    }
}
" : @"
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Features.{{entities}}.Entities;
using {{solution}}.Application.Common.Interfaces;

namespace {{solution}}.Application.Features.{{entities}}.Commands.Update;

public class Update{{entity}}Handler : IRequestHandler<Update{{entity}}Command>
{
    private readonly IUnitOfWork _uow;
    public Update{{entity}}Handler(IUnitOfWork uow) => _uow = uow;
    public async Task Handle(Update{{entity}}Command request, CancellationToken ct)
    {
        var entity = new {{entity}} { Id = request.Id };
        await _uow.{{entity}}Repository.UpdateAsync(entity);
        await _uow.SaveChangesAsync();
    }
}
"));
        var updateValidatorPath = Path.Combine(updateDir, $"Update{entity}Validator.cs");
        if (!File.Exists(updateValidatorPath))
            File.WriteAllText(updateValidatorPath, Fill(noDb ? @"
using FluentValidation;

namespace {{solution}}.Application.Features.{{entities}}.Commands.Update;

public class Update{{entity}}Validator : AbstractValidator<Update{{entity}}Command>
{
    public Update{{entity}}Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
" : @"
using FluentValidation;

namespace {{solution}}.Application.Features.{{entities}}.Commands.Update;

public class Update{{entity}}Validator : AbstractValidator<Update{{entity}}Command>
{
    public Update{{entity}}Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
"));

        var deleteDir = Path.Combine(commandsDir, "Delete");
        Directory.CreateDirectory(deleteDir);
        var deleteCmdPath = Path.Combine(deleteDir, $"Delete{entity}Command.cs");
        if (!File.Exists(deleteCmdPath))
            File.WriteAllText(deleteCmdPath, Fill(@"
using MediatR;

namespace {{solution}}.Application.Features.{{entities}}.Commands.Delete;

public record Delete{{entity}}Command(int Id) : IRequest;
"));
        var deleteHandlerPath = Path.Combine(deleteDir, $"Delete{entity}Handler.cs");
        if (!File.Exists(deleteHandlerPath))
            File.WriteAllText(deleteHandlerPath, Fill(noDb ? @"
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace {{solution}}.Application.Features.{{entities}}.Commands.Delete;

public class Delete{{entity}}Handler : IRequestHandler<Delete{{entity}}Command>
{
    public async Task Handle(Delete{{entity}}Command request, CancellationToken ct)
    {
        // No database configured. Implement deletion logic here if needed.
        await Task.CompletedTask;
    }
}
" : @"
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Application.Common.Interfaces;

namespace {{solution}}.Application.Features.{{entities}}.Commands.Delete;

public class Delete{{entity}}Handler : IRequestHandler<Delete{{entity}}Command>
{
    private readonly IUnitOfWork _uow;
    public Delete{{entity}}Handler(IUnitOfWork uow) => _uow = uow;
    public async Task Handle(Delete{{entity}}Command request, CancellationToken ct)
    {
        var entity = await _uow.{{entity}}Repository.GetByIdAsync(request.Id);
        if (entity != null)
        {
            await _uow.{{entity}}Repository.DeleteAsync(entity);
            await _uow.SaveChangesAsync();
        }
    }
}
"));
        var deleteValidatorPath = Path.Combine(deleteDir, $"Delete{entity}Validator.cs");
        if (!File.Exists(deleteValidatorPath))
            File.WriteAllText(deleteValidatorPath, Fill(@"
using FluentValidation;

namespace {{solution}}.Application.Features.{{entities}}.Commands.Delete;

public class Delete{{entity}}Validator : AbstractValidator<Delete{{entity}}Command>
{
    public Delete{{entity}}Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
"));

        // Queries
        var getByIdDir = Path.Combine(queriesDir, "GetById");
        Directory.CreateDirectory(getByIdDir);
        var getByIdQueryPath = Path.Combine(getByIdDir, $"Get{entity}ByIdQuery.cs");
        if (!File.Exists(getByIdQueryPath))
            File.WriteAllText(getByIdQueryPath, Fill(@"
using MediatR;
using {{solution}}.Application.Features.{{entities}}.Models;

namespace {{solution}}.Application.Features.{{entities}}.Queries.GetById;

public record Get{{entity}}ByIdQuery(int Id) : IRequest<{{entity}}Model?>;
"));
        var getByIdHandlerPath = Path.Combine(getByIdDir, $"Get{entity}ByIdHandler.cs");
        if (!File.Exists(getByIdHandlerPath))
            File.WriteAllText(getByIdHandlerPath, Fill(noDb ? @"
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Application.Features.{{entities}}.Models;

namespace {{solution}}.Application.Features.{{entities}}.Queries.GetById;

public class Get{{entity}}ByIdHandler : IRequestHandler<Get{{entity}}ByIdQuery, {{entity}}Model?>
{
    public Task<{{entity}}Model?> Handle(Get{{entity}}ByIdQuery request, CancellationToken ct)
        => Task.FromResult<{{entity}}Model?>(null);
}
" : @"
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Features.{{entities}}.Entities;
using {{solution}}.Application.Common.Interfaces;
using {{solution}}.Application.Features.{{entities}}.Models;

namespace {{solution}}.Application.Features.{{entities}}.Queries.GetById;

public class Get{{entity}}ByIdHandler : IRequestHandler<Get{{entity}}ByIdQuery, {{entity}}Model?>
{
    private readonly IUnitOfWork _uow;
    public Get{{entity}}ByIdHandler(IUnitOfWork uow) => _uow = uow;
    public async Task<{{entity}}Model?> Handle(Get{{entity}}ByIdQuery request, CancellationToken ct)
    {
        var e = await _uow.{{entity}}Repository.GetByIdAsync(request.Id);
        return e is null ? null : e;
    }
}
"));
        var getByIdValidatorPath = Path.Combine(getByIdDir, $"Get{entity}ByIdValidator.cs");
        if (!File.Exists(getByIdValidatorPath))
            File.WriteAllText(getByIdValidatorPath, Fill(@"
using FluentValidation;

namespace {{solution}}.Application.Features.{{entities}}.Queries.GetById;

public class Get{{entity}}ByIdValidator : AbstractValidator<Get{{entity}}ByIdQuery>
{
    public Get{{entity}}ByIdValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
"));

        var getAllDir = Path.Combine(queriesDir, "GetAll");
        Directory.CreateDirectory(getAllDir);
        var getAllQueryPath = Path.Combine(getAllDir, $"Get{entity}AllQuery.cs");
        if (!File.Exists(getAllQueryPath))
            File.WriteAllText(getAllQueryPath, Fill(@"
using MediatR;
using System.Collections.Generic;
using {{solution}}.Application.Features.{{entities}}.Models;

namespace {{solution}}.Application.Features.{{entities}}.Queries.GetAll;

public record Get{{entity}}AllQuery() : IRequest<List<{{entity}}Model>>;
"));
        var getAllHandlerPath = Path.Combine(getAllDir, $"Get{entity}AllHandler.cs");
        if (!File.Exists(getAllHandlerPath))
            File.WriteAllText(getAllHandlerPath, Fill(noDb ? @"
using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Application.Features.{{entities}}.Models;

namespace {{solution}}.Application.Features.{{entities}}.Queries.GetAll;

public class Get{{entity}}AllHandler : IRequestHandler<Get{{entity}}AllQuery, List<{{entity}}Model>>
{
    public Task<List<{{entity}}Model>> Handle(Get{{entity}}AllQuery request, CancellationToken ct)
        => Task.FromResult(new List<{{entity}}Model>());
}
" : @"
using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Features.{{entities}}.Entities;
using {{solution}}.Application.Common.Interfaces;
using {{solution}}.Application.Features.{{entities}}.Models;

namespace {{solution}}.Application.Features.{{entities}}.Queries.GetAll;

public class Get{{entity}}AllHandler : IRequestHandler<Get{{entity}}AllQuery, List<{{entity}}Model>>
{
    private readonly IUnitOfWork _uow;
    public Get{{entity}}AllHandler(IUnitOfWork uow) => _uow = uow;
    public async Task<List<{{entity}}Model>> Handle(Get{{entity}}AllQuery request, CancellationToken ct)
        => (await _uow.{{entity}}Repository.GetAllAsync()).Select(e => (({{entity}}Model)e)!).ToList();
}
"));
        if (!noDb)
        {
            var getListDir = Path.Combine(queriesDir, "GetList");
            Directory.CreateDirectory(getListDir);
            var getListQueryPath = Path.Combine(getListDir, $"Get{entity}ListQuery.cs");
            if (!File.Exists(getListQueryPath))
                File.WriteAllText(getListQueryPath, Fill(@"
using MediatR;
using {{solution}}.Core.Common.Models;
using {{solution}}.Application.Features.{{entities}}.Models;

namespace {{solution}}.Application.Features.{{entities}}.Queries.GetList;

public record Get{{entity}}ListQuery(int Page = 1, int PageSize = 10) : IRequest<PagedResult<{{entity}}Model>>;
"));
            var getListHandlerPath = Path.Combine(getListDir, $"Get{entity}ListHandler.cs");
            if (!File.Exists(getListHandlerPath))
                File.WriteAllText(getListHandlerPath, Fill(@"
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Common.Models;
using {{solution}}.Core.Features.{{entities}}.Entities;
using {{solution}}.Application.Common.Interfaces;
using {{solution}}.Application.Features.{{entities}}.Models;

namespace {{solution}}.Application.Features.{{entities}}.Queries.GetList;

public class Get{{entity}}ListHandler : IRequestHandler<Get{{entity}}ListQuery, PagedResult<{{entity}}Model>>
{
    private readonly IUnitOfWork _uow;
    public Get{{entity}}ListHandler(IUnitOfWork uow) => _uow = uow;
    public async Task<PagedResult<{{entity}}Model>> Handle(Get{{entity}}ListQuery request, CancellationToken ct)
    {
        var res = await _uow.{{entity}}Repository.ListAsync(request.Page, request.PageSize);
        return new PagedResult<{{entity}}Model>(res.Items.Select(e => (({{entity}}Model)e)!).ToList(), res.TotalCount, res.Page, res.PageSize);
    }
}
"));
            var getListValidatorPath = Path.Combine(getListDir, $"Get{entity}ListValidator.cs");
            if (!File.Exists(getListValidatorPath))
                File.WriteAllText(getListValidatorPath, Fill(@"
using FluentValidation;

namespace {{solution}}.Application.Features.{{entities}}.Queries.GetList;

public class Get{{entity}}ListValidator : AbstractValidator<Get{{entity}}ListQuery>
{
    public Get{{entity}}ListValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).GreaterThan(0);
            }
}
"));
        }
    }
}
