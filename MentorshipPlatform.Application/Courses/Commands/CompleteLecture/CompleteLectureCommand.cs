using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.CompleteLecture;

public record CompleteLectureCommand(Guid LectureId) : IRequest<Result>;

public class CompleteLectureCommandValidator : AbstractValidator<CompleteLectureCommand>
{
    public CompleteLectureCommandValidator()
    {
        RuleFor(x => x.LectureId).NotEmpty();
    }
}

public class CompleteLectureCommandHandler : IRequestHandler<CompleteLectureCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CompleteLectureCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(CompleteLectureCommand request, CancellationToken cancellationToken)
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
        progress.MarkCompleted();

        // Recalculate completion percentage
        var totalLectures = await _context.CourseLectures
            .CountAsync(l => _context.CourseSections
                .Where(s => s.CourseId == enrollment.CourseId)
                .Select(s => s.Id)
                .Contains(l.SectionId), cancellationToken);

        var completedLectures = await _context.LectureProgresses
            .CountAsync(p => p.EnrollmentId == enrollment.Id && p.IsCompleted, cancellationToken);

        // +1 for the current one if it wasn't already saved
        if (!await _context.LectureProgresses.AnyAsync(p => p.EnrollmentId == enrollment.Id && p.LectureId == request.LectureId && p.IsCompleted, cancellationToken))
            completedLectures++;

        var percentage = totalLectures > 0 ? (decimal)completedLectures / totalLectures * 100 : 0;
        enrollment.UpdateProgress(percentage);
        enrollment.UpdateLastAccessed();

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
