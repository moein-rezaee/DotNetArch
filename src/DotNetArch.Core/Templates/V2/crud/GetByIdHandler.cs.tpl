using {{App}}.Application.Abstractions.Persistence;
using {{App}}.Application.Common.Exceptions;
using {{App}}.Application.Features.{{Plural}}.Dtos;
using {{App}}.Domain.Entities;
using MediatR;

namespace {{App}}.Application.Features.{{Plural}}.Queries.Get{{Entity}}ById;

internal sealed class Get{{Entity}}ByIdQueryHandler(IUnitOfWork unitOfWork) : IRequestHandler<Get{{Entity}}ByIdQuery, {{Entity}}Dto>
{
    public async Task<{{Entity}}Dto> Handle(Get{{Entity}}ByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.Repository<{{Entity}}>().GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof({{Entity}}), request.Id);

        return entity.ToDto();
    }
}
