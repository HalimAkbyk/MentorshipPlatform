using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Courses.Commands.UnsuspendCourse;

public record UnsuspendCourseCommand(Guid CourseId, string? Note) : IRequest<Result>;

public class UnsuspendCourseCommandValidator : AbstractValidator<UnsuspendCourseCommand>
{
    public UnsuspendCourseCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

public class UnsuspendCourseCommandHandler : IRequestHandler<UnsuspendCourseCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;
    private readonly INotificationService _notification;
    private readonly ILogger<UnsuspendCourseCommandHandler> _logger;

    public UnsuspendCourseCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history,
        INotificationService notification,
        ILogger<UnsuspendCourseCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
        _notification = notification;
        _logger = logger;
    }

    public async Task<Result> Handle(UnsuspendCourseCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var adminId = _currentUser.UserId.Value;

        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course == null)
            return Result.Failure("Course not found");

        if (course.Status != CourseStatus.Suspended)
            return Result.Failure("Sadece askıdaki kurslar yeniden yayına alınabilir");

        var oldStatus = course.Status.ToString();
        course.Unsuspend();

        var note = CourseAdminNote.Create(
            course.Id, null, adminId,
            AdminNoteType.CourseUnsuspended, null,
            request.Note ?? "Askı kaldırıldı");
        _context.CourseAdminNotes.Add(note);

        await _context.SaveChangesAsync(cancellationToken);

        await _history.LogAsync(
            "Course", course.Id, "CourseUnsuspended",
            oldStatus, course.Status.ToString(),
            $"Kurs askıdan kaldırıldı",
            adminId, "Admin", ct: cancellationToken);

        try
        {
            var mentor = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == course.MentorUserId, cancellationToken);
            if (mentor != null && !string.IsNullOrEmpty(mentor.Email))
            {
                await _notification.SendEmailAsync(
                    mentor.Email,
                    $"Kursunuz Tekrar Yayında: {course.Title}",
                    $"\"{course.Title}\" adlı kursunuz tekrar yayına alındı.",
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send unsuspend notification for course {CourseId}", course.Id);
        }

        return Result.Success();
    }
}
