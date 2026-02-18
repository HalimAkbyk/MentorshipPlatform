using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Courses.Commands.SuspendCourse;

public record SuspendCourseCommand(Guid CourseId, string Reason) : IRequest<Result>;

public class SuspendCourseCommandValidator : AbstractValidator<SuspendCourseCommand>
{
    public SuspendCourseCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000)
            .WithMessage("Askıya alma nedeni zorunludur");
    }
}

public class SuspendCourseCommandHandler : IRequestHandler<SuspendCourseCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;
    private readonly INotificationService _notification;
    private readonly ILogger<SuspendCourseCommandHandler> _logger;

    public SuspendCourseCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history,
        INotificationService notification,
        ILogger<SuspendCourseCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
        _notification = notification;
        _logger = logger;
    }

    public async Task<Result> Handle(SuspendCourseCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var adminId = _currentUser.UserId.Value;

        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course == null)
            return Result.Failure("Course not found");

        if (course.Status != CourseStatus.Published)
            return Result.Failure("Sadece yayındaki kurslar askıya alınabilir");

        var oldStatus = course.Status.ToString();
        course.Suspend();

        var note = CourseAdminNote.Create(
            course.Id, null, adminId,
            AdminNoteType.CourseSuspended, null,
            request.Reason);
        _context.CourseAdminNotes.Add(note);

        await _context.SaveChangesAsync(cancellationToken);

        await _history.LogAsync(
            "Course", course.Id, "CourseSuspended",
            oldStatus, course.Status.ToString(),
            $"Kurs askıya alındı: {request.Reason}",
            adminId, "Admin", ct: cancellationToken);

        // Create user notification for mentor
        try
        {
            var userNotification = UserNotification.Create(
                course.MentorUserId,
                "CourseModeration",
                $"Kursunuz Askıya Alındı: {course.Title}",
                $"\"{course.Title}\" adlı kursunuz admin tarafından askıya alındı. Neden: {request.Reason}",
                "Course", course.Id,
                $"course-moderation-{course.Id}");
            _context.UserNotifications.Add(userNotification);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create user notification for course suspend {CourseId}", course.Id);
        }

        try
        {
            var mentor = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == course.MentorUserId, cancellationToken);
            if (mentor != null && !string.IsNullOrEmpty(mentor.Email))
            {
                await _notification.SendEmailAsync(
                    mentor.Email,
                    $"Kursunuz Askıya Alındı: {course.Title}",
                    $"\"{course.Title}\" adlı kursunuz admin tarafından askıya alındı.\n\nNeden: {request.Reason}",
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send suspend notification for course {CourseId}", course.Id);
        }

        return Result.Success();
    }
}
