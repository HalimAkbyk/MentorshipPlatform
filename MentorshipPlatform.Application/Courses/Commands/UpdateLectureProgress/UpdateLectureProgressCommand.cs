using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Courses.Commands.UpdateLectureProgress;

public record UpdateLectureProgressCommand(
    Guid LectureId,
    int WatchedSec,
    int LastPositionSec) : IRequest<Result>;

public class UpdateLectureProgressCommandValidator : AbstractValidator<UpdateLectureProgressCommand>
{
    public UpdateLectureProgressCommandValidator()
    {
        RuleFor(x => x.LectureId).NotEmpty();
        RuleFor(x => x.WatchedSec).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LastPositionSec).GreaterThanOrEqualTo(0);
    }
}

public class UpdateLectureProgressCommandHandler : IRequestHandler<UpdateLectureProgressCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UpdateLectureProgressCommandHandler> _logger;

    public UpdateLectureProgressCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<UpdateLectureProgressCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result> Handle(UpdateLectureProgressCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result.Failure("User not authenticated");
        var studentId = _currentUser.UserId.Value;

        var lecture = await _context.CourseLectures
            .Include(l => l.Section)
            .FirstOrDefaultAsync(l => l.Id == request.LectureId, cancellationToken);
        if (lecture == null) return Result.Failure("Lecture not found");

        var enrollment = await _context.CourseEnrollments
            .FirstOrDefaultAsync(e => e.CourseId == lecture.Section.CourseId
                && e.StudentUserId == studentId
                && e.Status == CourseEnrollmentStatus.Active, cancellationToken);
        if (enrollment == null) return Result.Failure("Active enrollment not found");

        try
        {
            var progress = await _context.LectureProgresses
                .FirstOrDefaultAsync(p => p.EnrollmentId == enrollment.Id && p.LectureId == request.LectureId, cancellationToken);

            if (progress == null)
            {
                progress = LectureProgress.Create(enrollment.Id, request.LectureId);
                _context.LectureProgresses.Add(progress);
            }

            progress.UpdateProgress(request.WatchedSec, request.LastPositionSec);
            enrollment.UpdateLastAccessed();

            // Create or update VideoWatchLog for instructor performance tracking
            try
            {
                var courseId = lecture.Section.CourseId;
                var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);
                if (course?.InstructorId != null)
                {
                    var watchLog = await _context.VideoWatchLogs
                        .FirstOrDefaultAsync(w => w.LectureId == request.LectureId
                            && w.StudentId == studentId, cancellationToken);

                    if (watchLog == null)
                    {
                        watchLog = VideoWatchLog.Create(
                            request.LectureId,
                            courseId,
                            studentId,
                            course.InstructorId.Value,
                            DateTime.UtcNow,
                            lecture.DurationSec);
                        _context.VideoWatchLogs.Add(watchLog);
                    }
                    else
                    {
                        var completionPct = lecture.DurationSec > 0
                            ? (decimal)request.WatchedSec / lecture.DurationSec * 100
                            : 0;
                        watchLog.UpdateProgress(request.WatchedSec, completionPct);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update VideoWatchLog for lecture {LectureId}", request.LectureId);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Concurrent insert race condition on unique index {EnrollmentId, LectureId}.
            // Two simultaneous requests both saw progress==null and tried to INSERT.
            // This is non-critical — the next progress save will succeed since the record now exists.
            _logger.LogWarning(ex,
                "Progress save conflict for lecture {LectureId} (concurrent insert), will succeed on next save",
                request.LectureId);
        }

        return Result.Success();
    }
}
