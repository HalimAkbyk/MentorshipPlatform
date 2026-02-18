using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Queries.GetPendingReviewCourses;

public record PendingReviewCourseDto(
    Guid Id,
    string Title,
    string MentorName,
    string? MentorEmail,
    string? Category,
    int TotalLectures,
    int TotalDurationSec,
    decimal Price,
    string Currency,
    DateTime SubmittedAt,
    int RoundNumber,
    string? CoverImageUrl);

public record GetPendingReviewCoursesQuery() : IRequest<Result<List<PendingReviewCourseDto>>>;

public class GetPendingReviewCoursesQueryHandler : IRequestHandler<GetPendingReviewCoursesQuery, Result<List<PendingReviewCourseDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetPendingReviewCoursesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<PendingReviewCourseDto>>> Handle(GetPendingReviewCoursesQuery request, CancellationToken cancellationToken)
    {
        var courses = await _context.Courses
            .AsNoTracking()
            .Where(c => c.Status == CourseStatus.PendingReview)
            .Include(c => c.MentorUser)
            .Include(c => c.ReviewRounds)
            .ToListAsync(cancellationToken);

        var result = courses
            .Select(c =>
            {
                // Get the latest active round (Outcome == null means still pending)
                var activeRound = c.ReviewRounds
                    .Where(r => r.Outcome == null)
                    .OrderByDescending(r => r.RoundNumber)
                    .FirstOrDefault();

                // Fallback to the latest round if no active round exists
                var latestRound = activeRound ?? c.ReviewRounds
                    .OrderByDescending(r => r.RoundNumber)
                    .FirstOrDefault();

                return new PendingReviewCourseDto(
                    c.Id,
                    c.Title,
                    c.MentorUser.DisplayName,
                    c.MentorUser.Email,
                    c.Category,
                    c.TotalLectures,
                    c.TotalDurationSec,
                    c.Price,
                    c.Currency,
                    latestRound?.SubmittedAt ?? c.CreatedAt,
                    latestRound?.RoundNumber ?? 1,
                    c.CoverImageUrl);
            })
            .OrderBy(dto => dto.SubmittedAt) // Oldest first
            .ToList();

        return Result<List<PendingReviewCourseDto>>.Success(result);
    }
}
