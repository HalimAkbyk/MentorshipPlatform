using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Queries.GetCourseDetail;

public record CurriculumLectureDto(Guid Id, string Title, int DurationSec, bool IsPreview, string Type);
public record CurriculumSectionDto(Guid Id, string Title, int SortOrder, List<CurriculumLectureDto> Lectures);
public record CourseMentorDto(Guid UserId, string DisplayName, string? AvatarUrl, string? Bio, string? Headline, decimal RatingAvg, int RatingCount);

public record CourseDetailDto(
    Guid Id,
    string Title,
    string? ShortDescription,
    string? Description,
    string? CoverImageUrl,
    string? CoverImagePosition,
    string? CoverImageTransform,
    string? PromoVideoKey,
    decimal Price,
    string Currency,
    string Level,
    string? Language,
    string? Category,
    string? WhatYouWillLearnJson,
    string? RequirementsJson,
    string? TargetAudienceJson,
    int TotalLectures,
    int TotalDurationSec,
    decimal RatingAvg,
    int RatingCount,
    int EnrollmentCount,
    CourseMentorDto Mentor,
    List<CurriculumSectionDto> Curriculum,
    bool IsEnrolled,
    bool IsOwnCourse);

public record GetCourseDetailQuery(Guid CourseId) : IRequest<Result<CourseDetailDto>>;

public class GetCourseDetailQueryHandler : IRequestHandler<GetCourseDetailQuery, Result<CourseDetailDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetCourseDetailQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<CourseDetailDto>> Handle(GetCourseDetailQuery request, CancellationToken cancellationToken)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.MentorUser)
            .Include(c => c.Sections.OrderBy(s => s.SortOrder))
                .ThenInclude(s => s.Lectures.OrderBy(l => l.SortOrder))
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course == null) return Result<CourseDetailDto>.Failure("Course not found");
        if (course.Status != CourseStatus.Published) return Result<CourseDetailDto>.Failure("Course not available");

        // Get mentor profile
        var mentorProfile = await _context.MentorProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == course.MentorUserId, cancellationToken);

        var isEnrolled = false;
        var isOwnCourse = false;
        if (_currentUser.UserId.HasValue)
        {
            isEnrolled = await _context.CourseEnrollments
                .AnyAsync(e => e.CourseId == request.CourseId
                    && e.StudentUserId == _currentUser.UserId.Value
                    && e.Status == CourseEnrollmentStatus.Active, cancellationToken);
            isOwnCourse = course.MentorUserId == _currentUser.UserId.Value;
        }

        var mentor = new CourseMentorDto(
            course.MentorUser.Id, course.MentorUser.DisplayName, course.MentorUser.AvatarUrl,
            mentorProfile?.Bio, mentorProfile?.Headline,
            mentorProfile?.RatingAvg ?? 0, mentorProfile?.RatingCount ?? 0);

        var curriculum = course.Sections.Select(s => new CurriculumSectionDto(
            s.Id, s.Title, s.SortOrder,
            s.Lectures.Select(l => new CurriculumLectureDto(
                l.Id, l.Title, l.DurationSec, l.IsPreview, l.Type.ToString()
            )).ToList()
        )).ToList();

        var dto = new CourseDetailDto(
            course.Id, course.Title, course.ShortDescription, course.Description,
            course.CoverImageUrl, course.CoverImagePosition, course.CoverImageTransform, course.PromoVideoKey,
            course.Price, course.Currency, course.Level.ToString(),
            course.Language, course.Category,
            course.WhatYouWillLearnJson, course.RequirementsJson, course.TargetAudienceJson,
            course.TotalLectures, course.TotalDurationSec,
            course.RatingAvg, course.RatingCount, course.EnrollmentCount,
            mentor, curriculum, isEnrolled, isOwnCourse);

        return Result<CourseDetailDto>.Success(dto);
    }
}
