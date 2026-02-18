using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.SubmitForReview;

public record SubmitCourseForReviewCommand(Guid CourseId, string? MentorNotes) : IRequest<Result>;

public class SubmitCourseForReviewCommandValidator : AbstractValidator<SubmitCourseForReviewCommand>
{
    public SubmitCourseForReviewCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
    }
}

public class SubmitCourseForReviewCommandHandler : IRequestHandler<SubmitCourseForReviewCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;

    public SubmitCourseForReviewCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
    }

    public async Task<Result> Handle(SubmitCourseForReviewCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        var course = await _context.Courses
            .Include(c => c.Sections)
                .ThenInclude(s => s.Lectures)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course == null)
            return Result.Failure("Course not found");

        if (course.MentorUserId != userId)
            return Result.Failure("Not authorized");

        // Status must be Draft or RevisionRequested
        if (course.Status != CourseStatus.Draft && course.Status != CourseStatus.RevisionRequested)
            return Result.Failure("Kurs yalnızca Taslak veya Revizyon İstendi durumunda incelemeye gönderilebilir");

        // Must have at least 1 section with 1 lecture
        if (!course.Sections.Any() || !course.Sections.Any(s => s.Lectures.Any()))
            return Result.Failure("Kurs en az bir bölüm ve bir ders içermelidir");

        // Price must be > 0
        if (course.Price <= 0)
            return Result.Failure("Kurs fiyatı belirlenmeden incelemeye gönderilemez");

        // If coming from RevisionRequested, check if any flagged videos were changed
        if (course.Status == CourseStatus.RevisionRequested)
        {
            var flagCheckResult = await CheckFlaggedVideosChanged(course, request.MentorNotes, cancellationToken);
            if (!flagCheckResult.IsSuccess)
                return flagCheckResult;
        }

        // Recalculate stats
        var totalLectures = course.Sections.Sum(s => s.Lectures.Count);
        var totalDuration = course.Sections.Sum(s => s.Lectures.Sum(l => l.DurationSec));
        course.UpdateStats(totalDuration, totalLectures);

        var oldStatus = course.Status.ToString();
        course.SubmitForReview();

        // Create new CourseReviewRound
        var maxRound = await _context.CourseReviewRounds
            .Where(r => r.CourseId == course.Id)
            .MaxAsync(r => (int?)r.RoundNumber, cancellationToken) ?? 0;

        var round = CourseReviewRound.Create(
            course.Id,
            maxRound + 1,
            userId,
            request.MentorNotes);

        _context.CourseReviewRounds.Add(round);

        await _context.SaveChangesAsync(cancellationToken);

        await _history.LogAsync(
            "Course", course.Id, "StatusChanged",
            oldStatus, CourseStatus.PendingReview.ToString(),
            $"Kurs incelemeye gönderildi (Round {round.RoundNumber})",
            userId, "Mentor", ct: cancellationToken);

        return Result.Success();
    }

    private async Task<Result> CheckFlaggedVideosChanged(
        Course course, string? mentorNotes, CancellationToken cancellationToken)
    {
        // Load the latest reviewed round with its lecture comments
        var latestReviewedRound = await _context.CourseReviewRounds
            .Include(r => r.LectureComments)
            .Where(r => r.CourseId == course.Id && r.Outcome != null)
            .OrderByDescending(r => r.RoundNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestReviewedRound == null)
            return Result.Success();

        var flaggedComments = latestReviewedRound.LectureComments
            .Where(c => c.Flag != LectureReviewFlag.None)
            .ToList();

        if (!flaggedComments.Any())
            return Result.Success();

        // Build a lookup of current lectures
        var allLectures = course.Sections
            .SelectMany(s => s.Lectures)
            .ToDictionary(l => l.Id);

        var anyFlaggedVideoChanged = false;

        foreach (var comment in flaggedComments)
        {
            if (comment.LectureId == null)
            {
                // Lecture was deleted — counts as changed
                anyFlaggedVideoChanged = true;
                break;
            }

            if (!allLectures.TryGetValue(comment.LectureId.Value, out var lecture))
            {
                // Lecture no longer exists — counts as changed
                anyFlaggedVideoChanged = true;
                break;
            }

            // Check if the video key has changed from the snapshot
            if (lecture.VideoKey != comment.VideoKey)
            {
                anyFlaggedVideoChanged = true;
                break;
            }
        }

        if (!anyFlaggedVideoChanged && string.IsNullOrWhiteSpace(mentorNotes))
            return Result.Failure("Hiçbir riskli video değiştirilmedi. Açıklama yazmanız zorunludur.");

        return Result.Success();
    }
}
