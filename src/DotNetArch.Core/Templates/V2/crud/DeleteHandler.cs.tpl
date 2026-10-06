using {{App}}.Application.Abstractions.Persistence;
using {{App}}.Application.Common.Exceptions;
using {{App}}.Domain.Entities;
using MediatR;

namespace {{App}}.Application.Features.{{Plural}}.Commands.Delete{{Entity}};

internal sealed class Delete{{Entity}}CommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<Delete{{Entity}}Command>
{
    public async Task Handle(Delete{{Entity}}Command request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<{{Entity}}>();
        var entity = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof({{Entity}}), request.Id);

        repository.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
