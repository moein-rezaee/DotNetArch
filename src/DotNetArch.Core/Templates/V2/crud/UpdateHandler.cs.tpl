using {{App}}.Application.Abstractions.Persistence;
using {{App}}.Application.Common.Exceptions;
using {{App}}.Application.Features.{{Plural}}.Dtos;
using {{App}}.Domain.Entities;
using MediatR;

namespace {{App}}.Application.Features.{{Plural}}.Commands.Update{{Entity}};

internal sealed class Update{{Entity}}CommandHandler(IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<Update{{Entity}}Command, {{Entity}}Dto>
{
    public async Task<{{Entity}}Dto> Handle(Update{{Entity}}Command request, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.Repository<{{Entity}}>().GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof({{Entity}}), request.Id);

        entity.Rename(request.Name, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }
}
