using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Admin.Commands.AssignInstructor;

public record AssignInstructorCommand(Guid UserId) : IRequest<Result>;

public class AssignInstructorCommandValidator : AbstractValidator<AssignInstructorCommand>
{
    public AssignInstructorCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class AssignInstructorCommandHandler : IRequestHandler<AssignInstructorCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AssignInstructorCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(AssignInstructorCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
            return Result.Failure("Kullanici bulunamadi");

        if (user.InstructorStatus == InstructorStatus.Active)
            return Result.Failure("Bu kullanici zaten egitmen olarak atanmis");

        user.AssignAsInstructor();

        // Create MentorProfile if not exists (needed for offerings/bookings)
        var profileExists = await _context.MentorProfiles
            .AnyAsync(m => m.UserId == request.UserId, cancellationToken);

        if (!profileExists)
        {
            var profile = MentorProfile.Create(request.UserId, "", "");
            profile.UpdateProfile("", null, null);
            _context.MentorProfiles.Add(profile);
        }

        // Audit log
        var history = ProcessHistory.Create(
            entityType: "User",
            entityId: request.UserId,
            action: "InstructorAssigned",
            oldValue: null,
            newValue: "InstructorStatus=Active",
            description: $"Egitmen olarak atandi: {user.DisplayName} ({user.Email})",
            performedBy: _currentUser.UserId,
            performedByRole: "Admin");

        _context.ProcessHistories.Add(history);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
