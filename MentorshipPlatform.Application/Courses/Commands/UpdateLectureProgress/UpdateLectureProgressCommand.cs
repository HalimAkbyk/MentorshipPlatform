using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

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

    public UpdateLectureProgressCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
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

        var progress = await _context.LectureProgresses
            .FirstOrDefaultAsync(p => p.EnrollmentId == enrollment.Id && p.LectureId == request.LectureId, cancellationToken);

        if (progress == null)
        {
            progress = LectureProgress.Create(enrollment.Id, request.LectureId);
            _context.LectureProgresses.Add(progress);
        }

        progress.UpdateProgress(request.WatchedSec, request.LastPositionSec);
        enrollment.UpdateLastAccessed();
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
