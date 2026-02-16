using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Queries.GetPublicCourses;

public record PublicCourseDto(
    Guid Id,
    string Title,
    string? ShortDescription,
    string? CoverImageUrl,
    string? CoverImagePosition,
    string? CoverImageTransform,
    decimal Price,
    string Currency,
    string Level,
    string? Category,
    string MentorName,
    string? MentorAvatarUrl,
    int TotalLectures,
    int TotalDurationSec,
    decimal RatingAvg,
    int RatingCount,
    int EnrollmentCount);

public record GetPublicCoursesQuery(
    string? Search,
    string? Category,
    string? Level,
    string? SortBy,
    int Page,
    int PageSize) : IRequest<Result<PublicCoursesResponse>>;

public record PublicCoursesResponse(
    List<PublicCourseDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class GetPublicCoursesQueryHandler : IRequestHandler<GetPublicCoursesQuery, Result<PublicCoursesResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetPublicCoursesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PublicCoursesResponse>> Handle(GetPublicCoursesQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        var query = _context.Courses
            .AsNoTracking()
            .Include(c => c.MentorUser)
            .Where(c => c.Status == CourseStatus.Published);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(c => c.Title.ToLower().Contains(search)
                || (c.ShortDescription != null && c.ShortDescription.ToLower().Contains(search))
                || (c.Category != null && c.Category.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(c => c.Category == request.Category);

        if (!string.IsNullOrWhiteSpace(request.Level) && Enum.TryParse<CourseLevel>(request.Level, true, out var level))
            query = query.Where(c => c.Level == level);

        var totalCount = await query.CountAsync(cancellationToken);

        query = request.SortBy?.ToLower() switch
        {
            "popular" => query.OrderByDescending(c => c.EnrollmentCount),
            "highest-rated" => query.OrderByDescending(c => c.RatingAvg),
            "price-asc" => query.OrderBy(c => c.Price),
            "price-desc" => query.OrderByDescending(c => c.Price),
            _ => query.OrderByDescending(c => c.CreatedAt) // "newest" default
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new PublicCourseDto(
                c.Id, c.Title, c.ShortDescription, c.CoverImageUrl, c.CoverImagePosition, c.CoverImageTransform,
                c.Price, c.Currency, c.Level.ToString(), c.Category,
                c.MentorUser.DisplayName, c.MentorUser.AvatarUrl,
                c.TotalLectures, c.TotalDurationSec,
                c.RatingAvg, c.RatingCount, c.EnrollmentCount))
            .ToListAsync(cancellationToken);

        return Result<PublicCoursesResponse>.Success(
            new PublicCoursesResponse(items, totalCount, page, pageSize));
    }
}
