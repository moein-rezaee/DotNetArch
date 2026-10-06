using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Scopes.Commands.DeleteScope;

public sealed class DeleteScopeCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<DeleteScopeCommand>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(DeleteScopeCommand request, CancellationToken cancellationToken)
    {
        var scopeRepo = _unitOfWork.Repository<Scope>();
        var clientScopeRepo = _unitOfWork.Repository<ClientScope>();
        var mappingRepo = _unitOfWork.Repository<ScopePermission>();

        var scope = await scopeRepo.GetByIdAsync(request.Id, cancellationToken);
        if (scope is null)
        {
            throw new NotFoundException("Scope not found.");
        }

        var anyClient = clientScopeRepo.Query().Any(cs => cs.ScopeId == scope.Id);
        if (anyClient)
        {
            throw new BadRequestException("Scope is assigned to clients and cannot be deleted.", "scope_has_clients");
        }

        var mappings = mappingRepo.Query().Where(sp => sp.ScopeId == scope.Id).ToList();
        foreach (var sp in mappings)
        {
            mappingRepo.Remove(sp);
        }

        scopeRepo.Remove(scope);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

