using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Admin.Commands.SetOwner;

public record SetOwnerCommand(Guid UserId, bool IsOwner) : IRequest<Result>;

public class SetOwnerCommandHandler : IRequestHandler<SetOwnerCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SetOwnerCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(SetOwnerCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
            return Result.Failure("Kullanici bulunamadi");

        user.SetOwner(request.IsOwner);

        // If setting as owner, also ensure instructor role
        if (request.IsOwner && user.InstructorStatus != Domain.Enums.InstructorStatus.Active)
        {
            user.AssignAsInstructor();

            var profileExists = await _context.MentorProfiles
                .AnyAsync(m => m.UserId == request.UserId, cancellationToken);
            if (!profileExists)
            {
                var profile = MentorProfile.Create(request.UserId, "", "");
                _context.MentorProfiles.Add(profile);
            }
        }

        var history = ProcessHistory.Create(
            entityType: "User",
            entityId: request.UserId,
            action: "OwnerStatusChanged",
            oldValue: null,
            newValue: $"IsOwner={request.IsOwner}",
            description: $"Urun sahibi durumu degistirildi: {user.DisplayName} -> IsOwner={request.IsOwner}",
            performedBy: _currentUser.UserId,
            performedByRole: "Admin");

        _context.ProcessHistories.Add(history);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
