using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Queries.GetMyCourses;

public record MentorCourseDto(
    Guid Id,
    string Title,
    string? ShortDescription,
    string? CoverImageUrl,
    string? CoverImagePosition,
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

public record GetMyCoursesQuery : IRequest<Result<List<MentorCourseDto>>>;

public class GetMyCoursesQueryHandler : IRequestHandler<GetMyCoursesQuery, Result<List<MentorCourseDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyCoursesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<MentorCourseDto>>> Handle(GetMyCoursesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<List<MentorCourseDto>>.Failure("User not authenticated");

        var courses = await _context.Courses
            .AsNoTracking()
            .Where(c => c.MentorUserId == _currentUser.UserId.Value)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new MentorCourseDto(
                c.Id, c.Title, c.ShortDescription, c.CoverImageUrl, c.CoverImagePosition,
                c.Status.ToString(), c.Level.ToString(),
                c.Price, c.Currency, c.TotalLectures, c.TotalDurationSec,
                c.EnrollmentCount, c.RatingAvg, c.RatingCount, c.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<MentorCourseDto>>.Success(courses);
    }
}
