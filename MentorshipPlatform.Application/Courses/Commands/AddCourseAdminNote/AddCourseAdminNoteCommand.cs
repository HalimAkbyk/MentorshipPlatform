using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Courses.Commands.AddCourseAdminNote;

public record AddCourseAdminNoteCommand(
    Guid CourseId,
    Guid? LectureId,
    LectureReviewFlag? Flag,
    string Content) : IRequest<Result>;

public class AddCourseAdminNoteCommandValidator : AbstractValidator<AddCourseAdminNoteCommand>
{
    public AddCourseAdminNoteCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000)
            .WithMessage("Yorum içeriği zorunludur");
    }
}

public class AddCourseAdminNoteCommandHandler : IRequestHandler<AddCourseAdminNoteCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;
    private readonly INotificationService _notification;
    private readonly ILogger<AddCourseAdminNoteCommandHandler> _logger;

    public AddCourseAdminNoteCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history,
        INotificationService notification,
        ILogger<AddCourseAdminNoteCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
        _notification = notification;
        _logger = logger;
    }

    public async Task<Result> Handle(AddCourseAdminNoteCommand request, CancellationToken cancellationToken)
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

        string? lectureTitle = null;
        if (request.LectureId.HasValue)
        {
            var lecture = course.Sections
                .SelectMany(s => s.Lectures)
                .FirstOrDefault(l => l.Id == request.LectureId.Value);

            if (lecture == null)
                return Result.Failure("Lecture not found");

            lectureTitle = lecture.Title;
        }

        var noteType = request.LectureId.HasValue ? AdminNoteType.Flag : AdminNoteType.General;

        var note = CourseAdminNote.Create(
            course.Id,
            request.LectureId,
            adminId,
            noteType,
            request.Flag,
            request.Content,
            lectureTitle);
        _context.CourseAdminNotes.Add(note);

        await _context.SaveChangesAsync(cancellationToken);

        var action = request.LectureId.HasValue ? "LectureFlagged" : "CourseNoteAdded";
        await _history.LogAsync(
            "Course", course.Id, action,
            null, request.Flag?.ToString(),
            request.LectureId.HasValue
                ? $"Ders '{lectureTitle}' için admin notu eklendi"
                : $"Kurs '{course.Title}' için admin notu eklendi",
            adminId, "Admin", ct: cancellationToken);

        try
        {
            var mentor = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == course.MentorUserId, cancellationToken);
            if (mentor != null && !string.IsNullOrEmpty(mentor.Email))
            {
                string subject, body;
                if (request.LectureId.HasValue && request.Flag.HasValue)
                {
                    subject = $"Ders İşaretlendi: {lectureTitle}";
                    body = $"\"{course.Title}\" adlı kursunuzdaki \"{lectureTitle}\" dersi admin tarafından " +
                           $"'{request.Flag}' olarak işaretlendi.\n\nYorum: {request.Content}";
                }
                else
                {
                    subject = $"Kursunuz Hakkında Admin Notu: {course.Title}";
                    body = $"\"{course.Title}\" adlı kursunuz hakkında admin tarafından yorum bırakıldı.\n\nYorum: {request.Content}";
                }

                await _notification.SendEmailAsync(mentor.Email, subject, body, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send admin note notification for course {CourseId}", course.Id);
        }

        return Result.Success();
    }
}
