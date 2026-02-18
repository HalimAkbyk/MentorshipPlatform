using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.PublishCourse;

/// <summary>
/// Now routes through admin review instead of directly publishing.
/// Mentor clicks "Publish" → Course goes to PendingReview.
/// Accepts optional MentorNotes for resubmission scenarios.
/// </summary>
public record PublishCourseCommand(Guid CourseId, string? MentorNotes = null) : IRequest<Result>;

public class PublishCourseCommandValidator : AbstractValidator<PublishCourseCommand>
{
    public PublishCourseCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
    }
}

public class PublishCourseCommandHandler : IRequestHandler<PublishCourseCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;
    private readonly IAdminNotificationService _adminNotification;

    public PublishCourseCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history,
        IAdminNotificationService adminNotification)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
        _adminNotification = adminNotification;
    }

    public async Task<Result> Handle(PublishCourseCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var course = await _context.Courses
            .Include(c => c.Sections)
                .ThenInclude(s => s.Lectures)
            .Include(c => c.ReviewRounds)
                .ThenInclude(r => r.LectureComments)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course == null) return Result.Failure("Course not found");
        if (course.MentorUserId != _currentUser.UserId.Value) return Result.Failure("Not authorized");

        // Only Draft or RevisionRequested courses can be submitted for review
        if (course.Status != CourseStatus.Draft && course.Status != CourseStatus.RevisionRequested)
            return Result.Failure("Kurs yalnızca taslak veya revizyon istenen durumda onaya gönderilebilir");

        if (!course.Sections.Any() || !course.Sections.Any(s => s.Lectures.Any()))
            return Result.Failure("Kurs onaya gönderilmeden önce en az bir bölüm ve bir ders eklenmelidir");

        if (course.Price <= 0)
            return Result.Failure("Kurs fiyatı belirlenmeden onaya gönderilemez");

        // If coming from RevisionRequested, check if any flagged videos were changed
        if (course.Status == CourseStatus.RevisionRequested)
        {
            var latestReviewedRound = course.ReviewRounds
                .Where(r => r.Outcome != null)
                .OrderByDescending(r => r.RoundNumber)
                .FirstOrDefault();

            if (latestReviewedRound != null)
            {
                var flaggedComments = latestReviewedRound.LectureComments
                    .Where(c => c.Flag != LectureReviewFlag.None)
                    .ToList();

                if (flaggedComments.Any())
                {
                    var allLectures = course.Sections
                        .SelectMany(s => s.Lectures)
                        .ToDictionary(l => l.Id);

                    var anyFlaggedVideoChanged = flaggedComments.Any(fc =>
                    {
                        if (fc.LectureId == null) return true; // Lecture was deleted → considered "changed"
                        if (!allLectures.TryGetValue(fc.LectureId.Value, out var lecture)) return true; // Deleted
                        return lecture.VideoKey != fc.VideoKey; // Video was replaced
                    });

                    if (!anyFlaggedVideoChanged && string.IsNullOrWhiteSpace(request.MentorNotes))
                    {
                        return Result.Failure("Hiçbir riskli video değiştirilmedi. Açıklama yazmanız zorunludur.");
                    }
                }
            }
        }

        // Recalculate stats
        var totalLectures = course.Sections.Sum(s => s.Lectures.Count);
        var totalDuration = course.Sections.Sum(s => s.Lectures.Sum(l => l.DurationSec));
        course.UpdateStats(totalDuration, totalLectures);

        var oldStatus = course.Status.ToString();

        // Submit for review (or resubmit)
        if (course.Status == CourseStatus.RevisionRequested)
            course.ResubmitForReview();
        else
            course.SubmitForReview();

        // Create new review round
        var maxRound = course.ReviewRounds.Any()
            ? course.ReviewRounds.Max(r => r.RoundNumber)
            : 0;

        var round = CourseReviewRound.Create(
            course.Id,
            maxRound + 1,
            _currentUser.UserId.Value,
            request.MentorNotes);

        _context.CourseReviewRounds.Add(round);

        await _context.SaveChangesAsync(cancellationToken);

        await _history.LogAsync("Course", course.Id, "SubmittedForReview",
            oldStatus, "PendingReview",
            $"Kurs inceleme için gönderildi. Round: {round.RoundNumber}",
            _currentUser.UserId.Value, "Mentor",
            ct: cancellationToken);

        // Create grouped admin notification for pending course review
        await _adminNotification.CreateOrUpdateGroupedAsync(
            "CourseReview",
            "pending-course-reviews",
            count => ("Kurs İncelemeleri", $"Bekleyen {count} kurs incelemeniz var"),
            "CourseReview", course.Id,
            cancellationToken);

        return Result.Success();
    }
}
