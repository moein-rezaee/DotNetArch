using {{App}}.Application.Abstractions.Persistence;
using {{App}}.Application.Common.Exceptions;
using {{App}}.Application.Features.{{Plural}}.Dtos;
using {{App}}.Domain.Entities;
using MediatR;

namespace {{App}}.Application.Features.{{Plural}}.Actions.{{ActionName}}{{Entity}};

internal sealed class {{ActionName}}{{Entity}}CommandHandler(IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<{{ActionName}}{{Entity}}Command, {{Entity}}Dto>
{
    public async Task<{{Entity}}Dto> Handle({{ActionName}}{{Entity}}Command request, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.Repository<{{Entity}}>().GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof({{Entity}}), request.Id);

        entity.{{ActionName}}(clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }
}
