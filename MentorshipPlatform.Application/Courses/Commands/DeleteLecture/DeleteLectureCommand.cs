using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.DeleteLecture;

public record DeleteLectureCommand(Guid LectureId) : IRequest<Result>;

public class DeleteLectureCommandValidator : AbstractValidator<DeleteLectureCommand>
{
    public DeleteLectureCommandValidator()
    {
        RuleFor(x => x.LectureId).NotEmpty();
    }
}

public class DeleteLectureCommandHandler : IRequestHandler<DeleteLectureCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IStorageService _storage;

    public DeleteLectureCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IStorageService storage)
    {
        _context = context;
        _currentUser = currentUser;
        _storage = storage;
    }

    public async Task<Result> Handle(DeleteLectureCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result.Failure("User not authenticated");

        var lecture = await _context.CourseLectures
            .Include(l => l.Section).ThenInclude(s => s.Course)
            .FirstOrDefaultAsync(l => l.Id == request.LectureId, cancellationToken);

        if (lecture == null) return Result.Failure("Lecture not found");
        if (lecture.Section.Course.MentorUserId != _currentUser.UserId.Value) return Result.Failure("Not authorized");

        if (!string.IsNullOrEmpty(lecture.VideoKey))
        {
            try { await _storage.DeleteFileAsync(lecture.VideoKey, cancellationToken); } catch { }
        }

        var courseId = lecture.Section.CourseId;
        _context.CourseLectures.Remove(lecture);
        await _context.SaveChangesAsync(cancellationToken);

        // Recalculate course stats
        var course = await _context.Courses
            .Include(c => c.Sections).ThenInclude(s => s.Lectures)
            .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);
        if (course != null)
        {
            var totalLectures = course.Sections.Sum(s => s.Lectures.Count);
            var totalDuration = course.Sections.Sum(s => s.Lectures.Sum(l => l.DurationSec));
            course.UpdateStats(totalDuration, totalLectures);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
