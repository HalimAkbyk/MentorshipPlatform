using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Queries.GetCourseReviewDetail;

public record LectureCommentDto(
    Guid Id,
    Guid? LectureId,
    string LectureTitle,
    string? VideoKey,
    string Flag,
    string Comment,
    Guid CreatedByUserId,
    DateTime CreatedAt);

public record ReviewRoundDto(
    Guid Id,
    int RoundNumber,
    DateTime SubmittedAt,
    string? MentorNotes,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt,
    string? Outcome,
    string? AdminGeneralNotes,
    List<LectureCommentDto> LectureComments);

public record ReviewLectureDto(
    Guid Id,
    string Title,
    string? VideoUrl,
    int DurationSec,
    int SortOrder,
    bool IsPreview,
    string Type);

public record ReviewSectionDto(
    Guid Id,
    string Title,
    int SortOrder,
    List<ReviewLectureDto> Lectures);

public record CourseReviewDetailDto(
    Guid Id,
    string Title,
    string? Description,
    string? ShortDescription,
    string? CoverImageUrl,
    decimal Price,
    string Currency,
    string? Category,
    string Level,
    string? Language,
    string Status,
    string MentorName,
    string? MentorEmail,
    Guid MentorUserId,
    int TotalLectures,
    int TotalDurationSec,
    List<ReviewSectionDto> Sections,
    List<ReviewRoundDto> ReviewRounds);

public record GetCourseReviewDetailQuery(Guid CourseId) : IRequest<Result<CourseReviewDetailDto>>;

public class GetCourseReviewDetailQueryHandler : IRequestHandler<GetCourseReviewDetailQuery, Result<CourseReviewDetailDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IStorageService _storageService;

    public GetCourseReviewDetailQueryHandler(IApplicationDbContext context, IStorageService storageService)
    {
        _context = context;
        _storageService = storageService;
    }

    public async Task<Result<CourseReviewDetailDto>> Handle(GetCourseReviewDetailQuery request, CancellationToken cancellationToken)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.MentorUser)
            .Include(c => c.Sections.OrderBy(s => s.SortOrder))
                .ThenInclude(s => s.Lectures.OrderBy(l => l.SortOrder))
            .Include(c => c.ReviewRounds)
                .ThenInclude(r => r.LectureComments)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course == null)
            return Result<CourseReviewDetailDto>.Failure("Course not found");

        // Build sections with presigned video URLs
        var sections = new List<ReviewSectionDto>();
        foreach (var section in course.Sections)
        {
            var lectures = new List<ReviewLectureDto>();
            foreach (var lecture in section.Lectures)
            {
                string? videoUrl = null;
                if (!string.IsNullOrEmpty(lecture.VideoKey))
                {
                    videoUrl = await _storageService.GetPresignedUrlAsync(
                        lecture.VideoKey,
                        TimeSpan.FromHours(2),
                        cancellationToken);
                }

                lectures.Add(new ReviewLectureDto(
                    lecture.Id,
                    lecture.Title,
                    videoUrl,
                    lecture.DurationSec,
                    lecture.SortOrder,
                    lecture.IsPreview,
                    lecture.Type.ToString()));
            }

            sections.Add(new ReviewSectionDto(
                section.Id,
                section.Title,
                section.SortOrder,
                lectures));
        }

        // Build review rounds sorted by RoundNumber descending
        var reviewRounds = course.ReviewRounds
            .OrderByDescending(r => r.RoundNumber)
            .Select(r => new ReviewRoundDto(
                r.Id,
                r.RoundNumber,
                r.SubmittedAt,
                r.MentorNotes,
                r.ReviewedByUserId,
                r.ReviewedAt,
                r.Outcome?.ToString(),
                r.AdminGeneralNotes,
                r.LectureComments.Select(lc => new LectureCommentDto(
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

        var dto = new CourseReviewDetailDto(
            course.Id,
            course.Title,
            course.Description,
            course.ShortDescription,
            course.CoverImageUrl,
            course.Price,
            course.Currency,
            course.Category,
            course.Level.ToString(),
            course.Language,
            course.Status.ToString(),
            course.MentorUser.DisplayName,
            course.MentorUser.Email,
            course.MentorUserId,
            course.TotalLectures,
            course.TotalDurationSec,
            sections,
            reviewRounds);

        return Result<CourseReviewDetailDto>.Success(dto);
    }
}
