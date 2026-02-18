using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Courses.Commands.ToggleLectureActive;

public record ToggleLectureActiveCommand(
    Guid CourseId,
    Guid LectureId,
    bool IsActive,
    string? Reason) : IRequest<Result>;

public class ToggleLectureActiveCommandValidator : AbstractValidator<ToggleLectureActiveCommand>
{
    public ToggleLectureActiveCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.LectureId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(2000);

        RuleFor(x => x.Reason).NotEmpty()
            .WithMessage("Dersi pasife çekerken neden belirtilmelidir")
            .When(x => !x.IsActive);
    }
}

public class ToggleLectureActiveCommandHandler : IRequestHandler<ToggleLectureActiveCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;
    private readonly INotificationService _notification;
    private readonly ILogger<ToggleLectureActiveCommandHandler> _logger;

    public ToggleLectureActiveCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history,
        INotificationService notification,
        ILogger<ToggleLectureActiveCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
        _notification = notification;
        _logger = logger;
    }

    public async Task<Result> Handle(ToggleLectureActiveCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var adminId = _currentUser.UserId.Value;

        var course = await _context.Courses
            .Include(c => c.Sections)
                .ThenInclude(s => s.Lectures)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course == null)
            return Result.Failure("Course not found");

        var lecture = course.Sections
            .SelectMany(s => s.Lectures)
            .FirstOrDefault(l => l.Id == request.LectureId);

        if (lecture == null)
            return Result.Failure("Lecture not found");

        if (lecture.IsActive == request.IsActive)
            return Result.Failure(request.IsActive ? "Ders zaten aktif" : "Ders zaten pasif");

        var oldValue = lecture.IsActive ? "Active" : "Inactive";

        if (request.IsActive)
            lecture.Activate();
        else
            lecture.Deactivate();

        var noteType = request.IsActive ? AdminNoteType.LectureActivated : AdminNoteType.LectureDeactivated;
        var note = CourseAdminNote.Create(
            course.Id, lecture.Id, adminId,
            noteType, null,
            request.Reason ?? (request.IsActive ? "Ders aktife alındı" : "Ders pasife alındı"),
            lecture.Title);
        _context.CourseAdminNotes.Add(note);

        // Recalculate stats (exclude inactive lectures)
        var activeLectures = course.Sections
            .SelectMany(s => s.Lectures)
            .Where(l => l.IsActive);
        var totalLectures = activeLectures.Count();
        var totalDuration = activeLectures.Sum(l => l.DurationSec);
        course.UpdateStats(totalDuration, totalLectures);

        await _context.SaveChangesAsync(cancellationToken);

        await _history.LogAsync(
            "CourseLecture", lecture.Id, request.IsActive ? "LectureActivated" : "LectureDeactivated",
            oldValue, request.IsActive ? "Active" : "Inactive",
            $"Ders '{lecture.Title}' {(request.IsActive ? "aktife" : "pasife")} alındı. Kurs: {course.Title}",
            adminId, "Admin", ct: cancellationToken);

        try
        {
            var mentor = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == course.MentorUserId, cancellationToken);
            if (mentor != null && !string.IsNullOrEmpty(mentor.Email))
            {
                var action = request.IsActive ? "aktife alındı" : "pasife alındı";
                var body = $"\"{course.Title}\" adlı kursunuzdaki \"{lecture.Title}\" dersi admin tarafından {action}.";
                if (!string.IsNullOrEmpty(request.Reason))
                    body += $"\n\nNeden: {request.Reason}";

                await _notification.SendEmailAsync(
                    mentor.Email,
                    $"Ders {(request.IsActive ? "Aktife Alındı" : "Pasife Alındı")}: {lecture.Title}",
                    body,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send lecture toggle notification for lecture {LectureId}", lecture.Id);
        }

        return Result.Success();
    }
}
