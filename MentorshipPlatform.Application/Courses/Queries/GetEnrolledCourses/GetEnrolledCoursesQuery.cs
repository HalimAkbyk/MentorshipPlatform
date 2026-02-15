using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Queries.GetEnrolledCourses;

public record EnrolledCourseDto(
    Guid CourseId,
    string Title,
    string? CoverImageUrl,
    string MentorName,
    string? MentorAvatarUrl,
    decimal CompletionPercentage,
    DateTime? LastAccessedAt,
    int TotalLectures,
    int TotalDurationSec);

public record GetEnrolledCoursesQuery : IRequest<Result<List<EnrolledCourseDto>>>;

public class GetEnrolledCoursesQueryHandler : IRequestHandler<GetEnrolledCoursesQuery, Result<List<EnrolledCourseDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetEnrolledCoursesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<EnrolledCourseDto>>> Handle(GetEnrolledCoursesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<List<EnrolledCourseDto>>.Failure("User not authenticated");

        var enrollments = await _context.CourseEnrollments
            .AsNoTracking()
            .Include(e => e.Course).ThenInclude(c => c.MentorUser)
            .Where(e => e.StudentUserId == _currentUser.UserId.Value && e.Status == CourseEnrollmentStatus.Active)
            .OrderByDescending(e => e.LastAccessedAt ?? e.CreatedAt)
            .Select(e => new EnrolledCourseDto(
                e.CourseId, e.Course.Title, e.Course.CoverImageUrl,
                e.Course.MentorUser.DisplayName, e.Course.MentorUser.AvatarUrl,
                e.CompletionPercentage, e.LastAccessedAt,
                e.Course.TotalLectures, e.Course.TotalDurationSec))
            .ToListAsync(cancellationToken);

        return Result<List<EnrolledCourseDto>>.Success(enrollments);
    }
}
