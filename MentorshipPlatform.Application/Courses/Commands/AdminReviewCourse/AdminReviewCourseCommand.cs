using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Courses.Commands.AdminReviewCourse;

public record LectureCommentInput(Guid LectureId, LectureReviewFlag Flag, string Comment);

public record AdminReviewCourseCommand(
    Guid CourseId,
    ReviewOutcome Outcome,
    string? GeneralNotes,
    List<LectureCommentInput> LectureComments) : IRequest<Result>;

public class AdminReviewCourseCommandValidator : AbstractValidator<AdminReviewCourseCommand>
{
    public AdminReviewCourseCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Outcome).IsInEnum();

        RuleFor(x => x.LectureComments)
            .Must(c => c != null && c.Count > 0)
            .WithMessage("Revizyon istendiğinde en az bir ders yorumu gereklidir")
            .When(x => x.Outcome == ReviewOutcome.RevisionRequested);

        RuleFor(x => x.GeneralNotes)
            .NotEmpty()
            .WithMessage("Ret durumunda genel not zorunludur")
            .When(x => x.Outcome == ReviewOutcome.Rejected);
    }
}

public class AdminReviewCourseCommandHandler : IRequestHandler<AdminReviewCourseCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;
    private readonly INotificationService _notification;
    private readonly ILogger<AdminReviewCourseCommandHandler> _logger;

    public AdminReviewCourseCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history,
        INotificationService notification,
        ILogger<AdminReviewCourseCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
        _notification = notification;
        _logger = logger;
    }

    public async Task<Result> Handle(AdminReviewCourseCommand request, CancellationToken cancellationToken)
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

        // Status must be PendingReview
        if (course.Status != CourseStatus.PendingReview)
            return Result.Failure("Kurs inceleme bekliyor durumunda değil");

        // Find the active (unreviewed) round: latest round where Outcome is null
        var activeRound = await _context.CourseReviewRounds
            .Where(r => r.CourseId == course.Id && r.Outcome == null)
            .OrderByDescending(r => r.RoundNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeRound == null)
            return Result.Failure("Aktif inceleme turu bulunamadı");

        var oldStatus = course.Status.ToString();

        // Apply outcome
        switch (request.Outcome)
        {
            case ReviewOutcome.Approved:
                activeRound.Approve(adminId, request.GeneralNotes);
                course.ApproveReview();
                // Recalculate stats on approval
                var totalLectures = course.Sections.Sum(s => s.Lectures.Count);
                var totalDuration = course.Sections.Sum(s => s.Lectures.Sum(l => l.DurationSec));
                course.UpdateStats(totalDuration, totalLectures);
                break;

            case ReviewOutcome.Rejected:
                activeRound.Reject(adminId, request.GeneralNotes!);
                course.RejectReview();
                break;

            case ReviewOutcome.RevisionRequested:
                activeRound.RequestRevision(adminId, request.GeneralNotes);
                course.RequestRevision();
                break;
        }

        // Create lecture comments
        if (request.LectureComments != null)
        {
            // Build a lookup of all lectures in the course
            var allLectures = course.Sections
                .SelectMany(s => s.Lectures)
                .ToDictionary(l => l.Id);

            foreach (var input in request.LectureComments)
            {
                if (!allLectures.TryGetValue(input.LectureId, out var lecture))
                    return Result.Failure($"Lecture not found: {input.LectureId}");

                var comment = LectureReviewComment.Create(
                    activeRound.Id,
                    lecture.Id,
                    lecture.Title,
                    lecture.VideoKey,
                    input.Flag,
                    input.Comment,
                    adminId);

                _context.LectureReviewComments.Add(comment);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // ProcessHistory log
        await _history.LogAsync(
            "Course", course.Id, "ReviewCompleted",
            oldStatus, course.Status.ToString(),
            $"Kurs incelemesi tamamlandı: {request.Outcome} (Round {activeRound.RoundNumber})",
            adminId, "Admin", ct: cancellationToken);

        // Send notification email to mentor (try/catch, don't fail if email fails)
        try
        {
            var mentorUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == course.MentorUserId, cancellationToken);

            if (mentorUser != null && !string.IsNullOrEmpty(mentorUser.Email))
            {
                var (subject, body) = BuildEmailContent(course.Title, request.Outcome, request.GeneralNotes);
                await _notification.SendEmailAsync(mentorUser.Email, subject, body, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send review notification email for course {CourseId}", course.Id);
        }

        return Result.Success();
    }

    private static (string Subject, string Body) BuildEmailContent(
        string courseTitle, ReviewOutcome outcome, string? notes)
    {
        return outcome switch
        {
            ReviewOutcome.Approved => (
                $"Kursunuz Onaylandı: {courseTitle}",
                $"Tebrikler! \"{courseTitle}\" adlı kursunuz inceleme sürecini başarıyla tamamladı ve yayınlandı."),

            ReviewOutcome.Rejected => (
                $"Kursunuz Reddedildi: {courseTitle}",
                $"\"{courseTitle}\" adlı kursunuz inceleme sürecinde reddedildi.\n\nAdmin Notu: {notes}"),

            ReviewOutcome.RevisionRequested => (
                $"Kursunuz İçin Revizyon İstendi: {courseTitle}",
                $"\"{courseTitle}\" adlı kursunuz için revizyon talep edildi. Lütfen admin yorumlarını inceleyip gerekli düzenlemeleri yaparak tekrar gönderin."
                + (string.IsNullOrEmpty(notes) ? "" : $"\n\nAdmin Notu: {notes}")),

            _ => (
                $"Kurs İnceleme Sonucu: {courseTitle}",
                $"\"{courseTitle}\" adlı kursunuzun incelemesi tamamlandı.")
        };
    }
}
