using System.IO;

namespace DotNetArch.Scaffolding.Steps;

public class ApplicationStep : IScaffoldStep
{
    public void Execute(string solution, string entity, string provider, string basePath)
    {
        var appBase = Path.Combine(basePath, $"{solution}.Application", entity);
        Directory.CreateDirectory(appBase);

        // Create
        var createDir = Path.Combine(appBase, "Create");
        Directory.CreateDirectory(createDir);
        File.WriteAllText(Path.Combine(createDir, $"Create{entity}Command.cs"), $$"""
using MediatR;
using {{solution}}.Core.Domain.{{entity}};

namespace {{solution}}.Application.{{entity}}.Create;

public record Create{{entity}}Command({{entity}} Entity) : IRequest<{{entity}}>;
""");
        File.WriteAllText(Path.Combine(createDir, $"Create{entity}Handler.cs"), $$"""
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Domain.{{entity}};
using {{solution}}.Core.Interfaces;

namespace {{solution}}.Application.{{entity}}.Create;

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
""");
        File.WriteAllText(Path.Combine(createDir, $"Create{entity}Validator.cs"), $$"""
using FluentValidation;

namespace {{solution}}.Application.{{entity}}.Create;

public class Create{{entity}}Validator : AbstractValidator<Create{{entity}}Command>
{
    public Create{{entity}}Validator()
    {
        RuleFor(x => x.Entity.Id).GreaterThanOrEqualTo(0);
    }
}
""");

        // Update
        var updateDir = Path.Combine(appBase, "Update");
        Directory.CreateDirectory(updateDir);
        File.WriteAllText(Path.Combine(updateDir, $"Update{entity}Command.cs"), $$"""
using MediatR;
using {{solution}}.Core.Domain.{{entity}};

namespace {{solution}}.Application.{{entity}}.Update;

public record Update{{entity}}Command({{entity}} Entity) : IRequest;
""");
        File.WriteAllText(Path.Combine(updateDir, $"Update{entity}Handler.cs"), $$"""
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Domain.{{entity}};
using {{solution}}.Core.Interfaces;

namespace {{solution}}.Application.{{entity}}.Update;

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
""");
        File.WriteAllText(Path.Combine(updateDir, $"Update{entity}Validator.cs"), $$"""
using FluentValidation;

namespace {{solution}}.Application.{{entity}}.Update;

public class Update{{entity}}Validator : AbstractValidator<Update{{entity}}Command>
{
    public Update{{entity}}Validator()
    {
        RuleFor(x => x.Entity.Id).GreaterThan(0);
    }
}
""");

        // Delete
        var deleteDir = Path.Combine(appBase, "Delete");
        Directory.CreateDirectory(deleteDir);
        File.WriteAllText(Path.Combine(deleteDir, $"Delete{entity}Command.cs"), $$"""
using MediatR;

namespace {{solution}}.Application.{{entity}}.Delete;

public record Delete{{entity}}Command(int Id) : IRequest;
""");
        File.WriteAllText(Path.Combine(deleteDir, $"Delete{entity}Handler.cs"), $$"""
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Interfaces;

namespace {{solution}}.Application.{{entity}}.Delete;

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
""");
        File.WriteAllText(Path.Combine(deleteDir, $"Delete{entity}Validator.cs"), $$"""
using FluentValidation;

namespace {{solution}}.Application.{{entity}}.Delete;

public class Delete{{entity}}Validator : AbstractValidator<Delete{{entity}}Command>
{
    public Delete{{entity}}Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
""");

        // GetById
        var getByIdDir = Path.Combine(appBase, "GetById");
        Directory.CreateDirectory(getByIdDir);
        File.WriteAllText(Path.Combine(getByIdDir, $"Get{entity}ByIdQuery.cs"), $$"""
using MediatR;
using {{solution}}.Core.Domain.{{entity}};

namespace {{solution}}.Application.{{entity}}.GetById;

public record Get{{entity}}ByIdQuery(int Id) : IRequest<{{entity}}>;
""");
        File.WriteAllText(Path.Combine(getByIdDir, $"Get{entity}ByIdHandler.cs"), $$"""
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Domain.{{entity}};
using {{solution}}.Core.Interfaces;

namespace {{solution}}.Application.{{entity}}.GetById;

public class Get{{entity}}ByIdHandler : IRequestHandler<Get{{entity}}ByIdQuery, {{entity}}>
{
    private readonly IUnitOfWork _uow;
    public Get{{entity}}ByIdHandler(IUnitOfWork uow) => _uow = uow;
    public async Task<{{entity}}> Handle(Get{{entity}}ByIdQuery request, CancellationToken ct)
        => await _uow.{{entity}}Repository.GetByIdAsync(request.Id);
}
""");
        File.WriteAllText(Path.Combine(getByIdDir, $"Get{entity}ByIdValidator.cs"), $$"""
using FluentValidation;

namespace {{solution}}.Application.{{entity}}.GetById;

public class Get{{entity}}ByIdValidator : AbstractValidator<Get{{entity}}ByIdQuery>
{
    public Get{{entity}}ByIdValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
""");

        // GetAll
        var getAllDir = Path.Combine(appBase, "GetAll");
        Directory.CreateDirectory(getAllDir);
        File.WriteAllText(Path.Combine(getAllDir, $"Get{entity}AllQuery.cs"), $$"""
using MediatR;
using System.Collections.Generic;
using {{solution}}.Core.Domain.{{entity}};

namespace {{solution}}.Application.{{entity}}.GetAll;

public record Get{{entity}}AllQuery() : IRequest<List<{{entity}}>>;
""");
        File.WriteAllText(Path.Combine(getAllDir, $"Get{entity}AllHandler.cs"), $$"""
using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Domain.{{entity}};
using {{solution}}.Core.Interfaces;

namespace {{solution}}.Application.{{entity}}.GetAll;

public class Get{{entity}}AllHandler : IRequestHandler<Get{{entity}}AllQuery, List<{{entity}}>>
{
    private readonly IUnitOfWork _uow;
    public Get{{entity}}AllHandler(IUnitOfWork uow) => _uow = uow;
    public async Task<List<{{entity}}>> Handle(Get{{entity}}AllQuery request, CancellationToken ct)
        => await _uow.{{entity}}Repository.GetAllAsync();
}
""");

        // GetList
        var getListDir = Path.Combine(appBase, "GetList");
        Directory.CreateDirectory(getListDir);
        File.WriteAllText(Path.Combine(getListDir, $"Get{entity}ListQuery.cs"), $$"""
using MediatR;
using {{solution}}.Core.Common;
using {{solution}}.Core.Domain.{{entity}};

namespace {{solution}}.Application.{{entity}}.GetList;

public record Get{{entity}}ListQuery(int Page = 1, int PageSize = 10) : IRequest<PagedResult<{{entity}}>>;
""");
        File.WriteAllText(Path.Combine(getListDir, $"Get{entity}ListHandler.cs"), $$"""
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using {{solution}}.Core.Common;
using {{solution}}.Core.Domain.{{entity}};
using {{solution}}.Core.Interfaces;

namespace {{solution}}.Application.{{entity}}.GetList;

public class Get{{entity}}ListHandler : IRequestHandler<Get{{entity}}ListQuery, PagedResult<{{entity}}>>
{
    private readonly IUnitOfWork _uow;
    public Get{{entity}}ListHandler(IUnitOfWork uow) => _uow = uow;
    public async Task<PagedResult<{{entity}}>> Handle(Get{{entity}}ListQuery request, CancellationToken ct)
        => await _uow.{{entity}}Repository.ListAsync(request.Page, request.PageSize);
}
""");
        File.WriteAllText(Path.Combine(getListDir, $"Get{entity}ListValidator.cs"), $$"""
using FluentValidation;

namespace {{solution}}.Application.{{entity}}.GetList;

public class Get{{entity}}ListValidator : AbstractValidator<Get{{entity}}ListQuery>
{
    public Get{{entity}}ListValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).GreaterThan(0);
    }
}
""");
    }
}
