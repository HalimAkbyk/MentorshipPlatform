using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Queries.GetEnrolledCourses;

public record EnrolledCourseDto(
    Guid CourseId,
    string Title,
    string? CoverImageUrl,
    string? CoverImagePosition,
    string? CoverImageTransform,
    string MentorName,
    string? MentorAvatarUrl,
    decimal CompletionPercentage,
    DateTime? LastAccessedAt,
    int TotalLectures,
    int TotalDurationSec);

public record GetEnrolledCoursesQuery(int Page = 1, int PageSize = 15) : IRequest<Result<PaginatedList<EnrolledCourseDto>>>;

public class GetEnrolledCoursesQueryHandler : IRequestHandler<GetEnrolledCoursesQuery, Result<PaginatedList<EnrolledCourseDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetEnrolledCoursesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<EnrolledCourseDto>>> Handle(GetEnrolledCoursesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PaginatedList<EnrolledCourseDto>>.Failure("User not authenticated");

        var page = PaginatedList<EnrolledCourseDto>.ClampPage(request.Page);
        var pageSize = PaginatedList<EnrolledCourseDto>.ClampPageSize(request.PageSize);

        var baseQuery = _context.CourseEnrollments
            .AsNoTracking()
            .Include(e => e.Course).ThenInclude(c => c.MentorUser)
            .Where(e => e.StudentUserId == _currentUser.UserId.Value && e.Status == CourseEnrollmentStatus.Active);

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var enrollments = await baseQuery
            .OrderByDescending(e => e.LastAccessedAt ?? e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EnrolledCourseDto(
                e.CourseId, e.Course.Title, e.Course.CoverImageUrl, e.Course.CoverImagePosition, e.Course.CoverImageTransform,
                e.Course.MentorUser.DisplayName, e.Course.MentorUser.AvatarUrl,
                e.CompletionPercentage, e.LastAccessedAt,
                e.Course.TotalLectures, e.Course.TotalDurationSec))
            .ToListAsync(cancellationToken);

        return Result<PaginatedList<EnrolledCourseDto>>.Success(
            new PaginatedList<EnrolledCourseDto>(enrollments, totalCount, page, pageSize));
    }
}
