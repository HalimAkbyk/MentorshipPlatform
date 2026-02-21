using MediatR;
using MentorshipPlatform.Application.Common.Constants;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Classes.Commands.CompleteGroupClass;

public record CompleteGroupClassCommand(Guid ClassId) : IRequest<Result<bool>>;

public class CompleteGroupClassCommandHandler
    : IRequestHandler<CompleteGroupClassCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _processHistory;
    private readonly IEmailService _emailService;
    private readonly ILogger<CompleteGroupClassCommandHandler> _logger;

    public CompleteGroupClassCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService processHistory,
        IEmailService emailService,
        ILogger<CompleteGroupClassCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _processHistory = processHistory;
        _emailService = emailService;
        _logger = logger;
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

        // Send completion emails to all attended students
        try
        {
            var mentorUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == mentorUserId, cancellationToken);

            var attendedEnrollments = groupClass.Enrollments
                .Where(e => e.Status == EnrollmentStatus.Attended)
                .ToList();

            var studentIds = attendedEnrollments.Select(e => e.StudentUserId).Distinct().ToList();
            var students = await _context.Users
                .AsNoTracking()
                .Where(u => studentIds.Contains(u.Id))
                .ToListAsync(cancellationToken);

            foreach (var student in students)
            {
                if (string.IsNullOrEmpty(student.Email)) continue;
                try
                {
                    await _emailService.SendTemplatedEmailAsync(
                        EmailTemplateKeys.GroupClassCompleted,
                        student.Email,
                        new Dictionary<string, string>
                        {
                            ["className"] = groupClass.Title,
                            ["mentorName"] = mentorUser?.DisplayName ?? "Mentor"
                        },
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send group class completion email to {Email}", student.Email);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send group class completion emails for {ClassId}", groupClass.Id);
        }

        return Result<bool>.Success(true);
    }
}
