using {{App}}.Application.Abstractions.Persistence;
using {{App}}.Application.Common.Exceptions;
using {{App}}.Application.Features.{{Plural}}.Dtos;
using {{App}}.Domain.Entities;
using MediatR;

namespace {{App}}.Application.Features.{{Plural}}.Actions.{{ActionName}}{{Entity}};

internal sealed class {{ActionName}}{{Entity}}QueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<{{ActionName}}{{Entity}}Query, {{Entity}}Dto>
{
    public async Task<{{Entity}}Dto> Handle({{ActionName}}{{Entity}}Query request, CancellationToken cancellationToken)
    {
        // TODO: shape the read model for '{{ActionName}}'. The default returns the aggregate as a DTO.
        var entity = await unitOfWork.Repository<{{Entity}}>().GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof({{Entity}}), request.Id);

        return entity.ToDto();
    }
}
