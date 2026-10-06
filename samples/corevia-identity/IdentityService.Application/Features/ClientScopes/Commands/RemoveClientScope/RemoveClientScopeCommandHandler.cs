using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.ClientScopes.Commands.RemoveClientScope;

public sealed class RemoveClientScopeCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<RemoveClientScopeCommand>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(RemoveClientScopeCommand request, CancellationToken cancellationToken)
    {
        var mappingRepo = _unitOfWork.Repository<ClientScope>();

        var mapping = mappingRepo.Query()
            .FirstOrDefault(cs => cs.ClientId == request.ClientId && cs.ScopeId == request.ScopeId);

        if (mapping is not null)
        {
            mappingRepo.Remove(mapping);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

