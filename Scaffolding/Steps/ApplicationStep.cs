using System.IO;

namespace DotNetArch.Scaffolding.Steps;

public class ApplicationStep : IScaffoldStep
{
    public void Execute(string solution, string entity, string provider, string basePath)
    {
        var appBase = Path.Combine(basePath, $"{solution}.Application", entity);
        Directory.CreateDirectory(appBase);

        var cmdDir = Path.Combine(appBase, "Commands");
        var queryDir = Path.Combine(appBase, "Queries");
        var valDir = Path.Combine(appBase, "Validators");
        Directory.CreateDirectory(cmdDir);
        Directory.CreateDirectory(queryDir);
        Directory.CreateDirectory(valDir);

        var createCommandFile = Path.Combine(cmdDir, $"Create{entity}Command.cs");
        var createContent = $$"""
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Domain.{{entity}};
using {{solution}}.Core.Interfaces;

namespace {{solution}}.Application.{{entity}}.Commands;

public record Create{{entity}}Command({{entity}} Entity) : IRequest<{{entity}}>;

public class Create{{entity}}Handler : IRequestHandler<Create{{entity}}Command, {{entity}}>
{
    private readonly IUnitOfWork _uow;
    public Create{{entity}}Handler(IUnitOfWork uow) => _uow = uow;
    public async Task<{{entity}}> Handle(Create{{entity}}Command request, CancellationToken ct)
    {
        await _uow.{{entity}}Repository.AddAsync(request.Entity);
        await _uow.SaveChangesAsync();
        return request.Entity;
    }
}
""";
        File.WriteAllText(createCommandFile, createContent);

        var updateCommandFile = Path.Combine(cmdDir, $"Update{entity}Command.cs");
        var updateContent = $$"""
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Domain.{{entity}};
using {{solution}}.Core.Interfaces;

namespace {{solution}}.Application.{{entity}}.Commands;

public record Update{{entity}}Command({{entity}} Entity) : IRequest;

public class Update{{entity}}Handler : IRequestHandler<Update{{entity}}Command>
{
    private readonly IUnitOfWork _uow;
    public Update{{entity}}Handler(IUnitOfWork uow) => _uow = uow;
    public async Task Handle(Update{{entity}}Command request, CancellationToken ct)
    {
        await _uow.{{entity}}Repository.UpdateAsync(request.Entity);
        await _uow.SaveChangesAsync();
    }
}
""";
        File.WriteAllText(updateCommandFile, updateContent);

        var deleteCommandFile = Path.Combine(cmdDir, $"Delete{entity}Command.cs");
        var deleteContent = $$"""
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Interfaces;

namespace {{solution}}.Application.{{entity}}.Commands;

public record Delete{{entity}}Command(int Id) : IRequest;

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
""";
        File.WriteAllText(deleteCommandFile, deleteContent);

        var getByIdFile = Path.Combine(queryDir, $"Get{entity}ByIdQuery.cs");
        var getByIdContent = $$"""
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Domain.{{entity}};
using {{solution}}.Core.Interfaces;

namespace {{solution}}.Application.{{entity}}.Queries;

public record Get{{entity}}ByIdQuery(int Id) : IRequest<{{entity}}>;

public class Get{{entity}}ByIdHandler : IRequestHandler<Get{{entity}}ByIdQuery, {{entity}}>
{
    private readonly IUnitOfWork _uow;
    public Get{{entity}}ByIdHandler(IUnitOfWork uow) => _uow = uow;
    public async Task<{{entity}}> Handle(Get{{entity}}ByIdQuery request, CancellationToken ct)
        => await _uow.{{entity}}Repository.GetByIdAsync(request.Id);
}
""";
        File.WriteAllText(getByIdFile, getByIdContent);

        var getAllFile = Path.Combine(queryDir, $"Get{entity}AllQuery.cs");
        var getAllContent = $$"""
using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Domain.{{entity}};
using {{solution}}.Core.Interfaces;

namespace {{solution}}.Application.{{entity}}.Queries;

public record Get{{entity}}AllQuery() : IRequest<List<{{entity}}>>;

public class Get{{entity}}AllHandler : IRequestHandler<Get{{entity}}AllQuery, List<{{entity}}>>
{
    private readonly IUnitOfWork _uow;
    public Get{{entity}}AllHandler(IUnitOfWork uow) => _uow = uow;
    public async Task<List<{{entity}}>> Handle(Get{{entity}}AllQuery request, CancellationToken ct)
        => await _uow.{{entity}}Repository.GetAllAsync();
}
""";
        File.WriteAllText(getAllFile, getAllContent);

        var getListFile = Path.Combine(queryDir, $"Get{entity}ListQuery.cs");
        var getListContent = $$"""
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Common;
using {{solution}}.Core.Domain.{{entity}};
using {{solution}}.Core.Interfaces;

namespace {{solution}}.Application.{{entity}}.Queries;

public record Get{{entity}}ListQuery(int Page = 1, int PageSize = 10) : IRequest<PagedResult<{{entity}}>>;

public class Get{{entity}}ListHandler : IRequestHandler<Get{{entity}}ListQuery, PagedResult<{{entity}}>>
{
    private readonly IUnitOfWork _uow;
    public Get{{entity}}ListHandler(IUnitOfWork uow) => _uow = uow;
    public async Task<PagedResult<{{entity}}>> Handle(Get{{entity}}ListQuery request, CancellationToken ct)
        => await _uow.{{entity}}Repository.ListAsync(request.Page, request.PageSize);
}
""";
        File.WriteAllText(getListFile, getListContent);

        var validatorFile = Path.Combine(valDir, $"{entity}Validator.cs");
        var validatorContent = $$"""
using FluentValidation;
using {{solution}}.Core.Domain.{{entity}};

namespace {{solution}}.Application.{{entity}}.Validators;

public class {{entity}}Validator : AbstractValidator<{{entity}}>
{
    public {{entity}}Validator()
    {
        RuleFor(x => x.Id).GreaterThanOrEqualTo(0);
    }
}
""";
        File.WriteAllText(validatorFile, validatorContent);
    }
}
