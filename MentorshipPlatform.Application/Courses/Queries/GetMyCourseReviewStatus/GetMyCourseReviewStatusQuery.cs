using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Queries.GetMyCourseReviewStatus;

public record MyCourseReviewLectureCommentDto(
    Guid Id,
    Guid? LectureId,
    string LectureTitle,
    string? VideoKey,
    string Flag,
    string Comment,
    Guid CreatedByUserId,
    DateTime CreatedAt);

public record MyCourseReviewRoundDto(
    Guid Id,
    int RoundNumber,
    DateTime SubmittedAt,
    string? MentorNotes,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt,
    string? Outcome,
    string? AdminGeneralNotes,
    List<MyCourseReviewLectureCommentDto> LectureComments);

public record MyCourseReviewStatusDto(
    Guid CourseId,
    string Title,
    string Status,
    MyCourseReviewRoundDto? LatestRound,
    List<MyCourseReviewRoundDto> AllRounds);

public record GetMyCourseReviewStatusQuery(Guid CourseId) : IRequest<Result<MyCourseReviewStatusDto>>;

public class GetMyCourseReviewStatusQueryHandler : IRequestHandler<GetMyCourseReviewStatusQuery, Result<MyCourseReviewStatusDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyCourseReviewStatusQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<MyCourseReviewStatusDto>> Handle(GetMyCourseReviewStatusQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<MyCourseReviewStatusDto>.Failure("User not authenticated");

        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.ReviewRounds)
                .ThenInclude(r => r.LectureComments)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course == null)
            return Result<MyCourseReviewStatusDto>.Failure("Course not found");

        // Validate ownership
        if (course.MentorUserId != _currentUser.UserId.Value)
            return Result<MyCourseReviewStatusDto>.Failure("You do not have access to this course");

        // Map rounds with comments to DTOs
        var allRounds = course.ReviewRounds
            .OrderByDescending(r => r.RoundNumber)
            .Select(r => new MyCourseReviewRoundDto(
                r.Id,
                r.RoundNumber,
                r.SubmittedAt,
                r.MentorNotes,
                r.ReviewedByUserId,
                r.ReviewedAt,
                r.Outcome?.ToString(),
                r.AdminGeneralNotes,
                r.LectureComments.Select(lc => new MyCourseReviewLectureCommentDto(
                    lc.Id,
                    lc.LectureId,
                    lc.LectureTitle,
                    lc.VideoKey,
                    lc.Flag.ToString(),
                    lc.Comment,
                    lc.CreatedByUserId,
                    lc.CreatedAt
                )).ToList()))
            .ToList();

        // LatestRound = highest RoundNumber
        var latestRound = allRounds.FirstOrDefault();

        var dto = new MyCourseReviewStatusDto(
            course.Id,
            course.Title,
            course.Status.ToString(),
            latestRound,
            allRounds);

        return Result<MyCourseReviewStatusDto>.Success(dto);
    }
}
