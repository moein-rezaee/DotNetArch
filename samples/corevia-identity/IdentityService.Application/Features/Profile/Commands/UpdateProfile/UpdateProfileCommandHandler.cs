using System;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Profile.Models;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Profile.Commands.UpdateProfile;

public sealed class UpdateProfileCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateProfileCommand>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.UserId, cancellationToken)
                   ?? throw new NotFoundException("User profile not found.");

        var profileRepo = _unitOfWork.Repository<UserProfile>();
        var profile = await profileRepo.FirstOrDefaultAsync(x => x.UserId == request.UserId, cancellationToken);

        if (profile is null)
        {
            profile = new UserProfile
            {
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            await profileRepo.AddAsync(profile, cancellationToken);
        }

        profile.FirstName = request.Request.FirstName?.Trim();
        profile.LastName = request.Request.LastName?.Trim();
        profile.Email = request.Request.Email?.Trim();
        profile.AvatarUrl = request.Request.AvatarUrl?.Trim();
        profile.UpdatedAt = DateTime.UtcNow;

        user.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
