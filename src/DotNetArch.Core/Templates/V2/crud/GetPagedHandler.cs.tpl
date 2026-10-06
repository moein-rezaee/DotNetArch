using {{App}}.Application.Abstractions.Persistence;
using {{App}}.Application.Common.Pagination;
using {{App}}.Application.Features.{{Plural}}.Dtos;
using {{App}}.Domain.Entities;
using MediatR;

namespace {{App}}.Application.Features.{{Plural}}.Queries.Get{{Plural}};

internal sealed class Get{{Plural}}QueryHandler(IUnitOfWork unitOfWork) : IRequestHandler<Get{{Plural}}Query, PagedResult<{{Entity}}Dto>>
{
    public async Task<PagedResult<{{Entity}}Dto>> Handle(Get{{Plural}}Query request, CancellationToken cancellationToken)
    {
        var page = await unitOfWork.Repository<{{Entity}}>().GetPagedAsync(request.PageNumber, request.PageSize, cancellationToken: cancellationToken);
        return page.Map(entity => entity.ToDto());
    }
}
