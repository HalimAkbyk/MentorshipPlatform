using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Queries.GetCourseForEdit;

public record LectureEditDto(
    Guid Id,
    string Title,
    string? Description,
    string? VideoKey,
    int DurationSec,
    int SortOrder,
    bool IsPreview,
    string Type,
    string? TextContent);

public record SectionEditDto(
    Guid Id,
    string Title,
    int SortOrder,
    List<LectureEditDto> Lectures);

public record CourseEditDto(
    Guid Id,
    string Title,
    string? ShortDescription,
    string? Description,
    decimal Price,
    string Currency,
    string Status,
    string Level,
    string? Language,
    string? Category,
    string? CoverImageUrl,
    string? CoverImagePosition,
    string? PromoVideoKey,
    string? WhatYouWillLearnJson,
    string? RequirementsJson,
    string? TargetAudienceJson,
    int TotalLectures,
    int TotalDurationSec,
    int EnrollmentCount,
    List<SectionEditDto> Sections);

public record GetCourseForEditQuery(Guid CourseId) : IRequest<Result<CourseEditDto>>;

public class GetCourseForEditQueryHandler : IRequestHandler<GetCourseForEditQuery, Result<CourseEditDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetCourseForEditQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<CourseEditDto>> Handle(GetCourseForEditQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<CourseEditDto>.Failure("User not authenticated");

        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Sections.OrderBy(s => s.SortOrder))
                .ThenInclude(s => s.Lectures.OrderBy(l => l.SortOrder))
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course == null) return Result<CourseEditDto>.Failure("Course not found");
        if (course.MentorUserId != _currentUser.UserId.Value) return Result<CourseEditDto>.Failure("Not authorized");

        var dto = new CourseEditDto(
            course.Id, course.Title, course.ShortDescription, course.Description,
            course.Price, course.Currency, course.Status.ToString(), course.Level.ToString(),
            course.Language, course.Category, course.CoverImageUrl, course.CoverImagePosition, course.PromoVideoKey,
            course.WhatYouWillLearnJson, course.RequirementsJson, course.TargetAudienceJson,
            course.TotalLectures, course.TotalDurationSec, course.EnrollmentCount,
            course.Sections.Select(s => new SectionEditDto(
                s.Id, s.Title, s.SortOrder,
                s.Lectures.Select(l => new LectureEditDto(
                    l.Id, l.Title, l.Description, l.VideoKey, l.DurationSec,
                    l.SortOrder, l.IsPreview, l.Type.ToString(), l.TextContent
                )).ToList()
            )).ToList());

        return Result<CourseEditDto>.Success(dto);
    }
}
