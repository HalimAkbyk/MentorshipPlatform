using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Queries.GetMyCourses;

public record MentorCourseDto(
    Guid Id,
    string Title,
    string? ShortDescription,
    string? CoverImageUrl,
    string? CoverImagePosition,
    string? CoverImageTransform,
    string Status,
    string Level,
    decimal Price,
    string Currency,
    int TotalLectures,
    int TotalDurationSec,
    int EnrollmentCount,
    decimal RatingAvg,
    int RatingCount,
    DateTime CreatedAt);

public record GetMyCoursesQuery(int Page = 1, int PageSize = 15) : IRequest<Result<PaginatedList<MentorCourseDto>>>;

public class GetMyCoursesQueryHandler : IRequestHandler<GetMyCoursesQuery, Result<PaginatedList<MentorCourseDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyCoursesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<MentorCourseDto>>> Handle(GetMyCoursesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PaginatedList<MentorCourseDto>>.Failure("User not authenticated");

        var page = PaginatedList<MentorCourseDto>.ClampPage(request.Page);
        var pageSize = PaginatedList<MentorCourseDto>.ClampPageSize(request.PageSize);

        var query = _context.Courses
            .AsNoTracking()
            .Where(c => c.MentorUserId == _currentUser.UserId.Value)
            .OrderByDescending(c => c.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var courses = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new MentorCourseDto(
                c.Id, c.Title, c.ShortDescription, c.CoverImageUrl, c.CoverImagePosition, c.CoverImageTransform,
                c.Status.ToString(), c.Level.ToString(),
                c.Price, c.Currency, c.TotalLectures, c.TotalDurationSec,
                c.EnrollmentCount, c.RatingAvg, c.RatingCount, c.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<PaginatedList<MentorCourseDto>>.Success(
            new PaginatedList<MentorCourseDto>(courses, totalCount, page, pageSize));
    }
}
