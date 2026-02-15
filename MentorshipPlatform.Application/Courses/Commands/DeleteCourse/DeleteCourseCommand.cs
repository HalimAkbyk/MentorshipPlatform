using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.DeleteCourse;

public record DeleteCourseCommand(Guid CourseId) : IRequest<Result>;

public class DeleteCourseCommandValidator : AbstractValidator<DeleteCourseCommand>
{
    public DeleteCourseCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
    }
}

public class DeleteCourseCommandHandler : IRequestHandler<DeleteCourseCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IStorageService _storage;

    public DeleteCourseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IStorageService storage)
    {
        _context = context;
        _currentUser = currentUser;
        _storage = storage;
    }

    public async Task<Result> Handle(DeleteCourseCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result.Failure("User not authenticated");

        var course = await _context.Courses
            .Include(c => c.Sections)
                .ThenInclude(s => s.Lectures)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course == null) return Result.Failure("Course not found");
        if (course.MentorUserId != _currentUser.UserId.Value) return Result.Failure("Not authorized");
        if (course.Status != CourseStatus.Draft)
            return Result.Failure("Sadece taslak durumdaki kurslar silinebilir");

        var hasEnrollments = await _context.CourseEnrollments
            .AnyAsync(e => e.CourseId == request.CourseId && e.Status == CourseEnrollmentStatus.Active, cancellationToken);
        if (hasEnrollments)
            return Result.Failure("Aktif kayıtları olan kurslar silinemez");

        // Delete S3 video files
        foreach (var section in course.Sections)
        {
            foreach (var lecture in section.Lectures)
            {
                if (!string.IsNullOrEmpty(lecture.VideoKey))
                {
                    try { await _storage.DeleteFileAsync(lecture.VideoKey, cancellationToken); } catch { }
                }
            }
        }

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
