using {{App}}.Application.Abstractions.Persistence;
using {{App}}.Application.Features.{{Plural}}.Dtos;
using {{App}}.Domain.Entities;
using MediatR;

namespace {{App}}.Application.Features.{{Plural}}.Commands.Create{{Entity}};

internal sealed class Create{{Entity}}CommandHandler(IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<Create{{Entity}}Command, {{Entity}}Dto>
{
    public async Task<{{Entity}}Dto> Handle(Create{{Entity}}Command request, CancellationToken cancellationToken)
    {
        var entity = {{Entity}}.Create(request.Name, clock.GetUtcNow().UtcDateTime);

        await unitOfWork.Repository<{{Entity}}>().AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }
}
