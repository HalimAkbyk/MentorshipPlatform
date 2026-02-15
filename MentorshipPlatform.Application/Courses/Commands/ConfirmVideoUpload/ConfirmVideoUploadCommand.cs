using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.ConfirmVideoUpload;

public record ConfirmVideoUploadCommand(
    Guid LectureId,
    string VideoKey,
    int DurationSec) : IRequest<Result>;

public class ConfirmVideoUploadCommandValidator : AbstractValidator<ConfirmVideoUploadCommand>
{
    public ConfirmVideoUploadCommandValidator()
    {
        RuleFor(x => x.LectureId).NotEmpty();
        RuleFor(x => x.VideoKey).NotEmpty();
        RuleFor(x => x.DurationSec).GreaterThanOrEqualTo(0);
    }
}

public class ConfirmVideoUploadCommandHandler : IRequestHandler<ConfirmVideoUploadCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ConfirmVideoUploadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ConfirmVideoUploadCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result.Failure("User not authenticated");

        var lecture = await _context.CourseLectures
            .Include(l => l.Section).ThenInclude(s => s.Course)
            .FirstOrDefaultAsync(l => l.Id == request.LectureId, cancellationToken);

        if (lecture == null) return Result.Failure("Lecture not found");
        if (lecture.Section.Course.MentorUserId != _currentUser.UserId.Value) return Result.Failure("Not authorized");

        lecture.SetVideoKey(request.VideoKey, request.DurationSec);

        // Recalculate course stats
        var course = lecture.Section.Course;
        var allLectures = await _context.CourseLectures
            .Where(l => _context.CourseSections
                .Where(s => s.CourseId == course.Id)
                .Select(s => s.Id)
                .Contains(l.SectionId))
            .ToListAsync(cancellationToken);

        course.UpdateStats(allLectures.Sum(l => l.DurationSec), allLectures.Count);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
