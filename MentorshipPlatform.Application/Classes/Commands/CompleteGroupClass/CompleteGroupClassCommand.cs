using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Classes.Commands.CompleteGroupClass;

public record CompleteGroupClassCommand(Guid ClassId) : IRequest<Result<bool>>;

public class CompleteGroupClassCommandHandler
    : IRequestHandler<CompleteGroupClassCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _processHistory;

    public CompleteGroupClassCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService processHistory)
    {
        _context = context;
        _currentUser = currentUser;
        _processHistory = processHistory;
    }

    public async Task<Result<bool>> Handle(
        CompleteGroupClassCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<bool>.Failure("Oturum açmanız gerekiyor");

        var mentorUserId = _currentUser.UserId.Value;

        var groupClass = await _context.GroupClasses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);

        if (groupClass == null)
            return Result<bool>.Failure("Grup dersi bulunamadı");

        if (groupClass.MentorUserId != mentorUserId)
            return Result<bool>.Failure("Yalnızca kendi derslerinizi tamamlayabilirsiniz");

        if (groupClass.Status != ClassStatus.Published)
            return Result<bool>.Failure("Yalnızca aktif dersler tamamlanabilir");

        groupClass.Complete();

        // Mark confirmed enrollments as attended
        foreach (var enrollment in groupClass.Enrollments
            .Where(e => e.Status == EnrollmentStatus.Confirmed))
        {
            enrollment.MarkAttended();
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _processHistory.LogAsync(
            "GroupClass", groupClass.Id, "Completed",
            "Published", "Completed",
            "Grup dersi tamamlandi olarak isaretlendi.",
            mentorUserId, "Mentor",
            ct: cancellationToken);

        return Result<bool>.Success(true);
    }
}
