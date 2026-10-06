using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Profile.Models;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Profile.Queries.GetProfile;

public sealed class GetProfileQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetProfileQuery, UserProfileResponse>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<UserProfileResponse> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.UserId, cancellationToken)
                   ?? throw new NotFoundException("User profile not found.");

        var profile = await _unitOfWork.Repository<UserProfile>()
            .FirstOrDefaultAsync(x => x.UserId == request.UserId, cancellationToken);

        return new UserProfileResponse(
            user.Id,
            user.PhoneNumber,
            user.CreatedAt,
            user.UpdatedAt,
            profile?.FirstName,
            profile?.LastName,
            profile?.Email,
            profile?.AvatarUrl);
    }
}
