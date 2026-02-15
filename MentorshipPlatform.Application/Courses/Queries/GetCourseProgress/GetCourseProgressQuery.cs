using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Queries.GetCourseProgress;

public record LectureProgressDto(Guid LectureId, string LectureTitle, bool IsCompleted, int WatchedSec, int LastPositionSec);
public record SectionProgressDto(Guid SectionId, string SectionTitle, List<LectureProgressDto> Lectures);

public record CourseProgressDto(
    decimal CompletionPercentage,
    int TotalLectures,
    int CompletedLectures,
    List<SectionProgressDto> Sections);

public record GetCourseProgressQuery(Guid CourseId) : IRequest<Result<CourseProgressDto>>;

public class GetCourseProgressQueryHandler : IRequestHandler<GetCourseProgressQuery, Result<CourseProgressDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetCourseProgressQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<CourseProgressDto>> Handle(GetCourseProgressQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result<CourseProgressDto>.Failure("User not authenticated");

        var enrollment = await _context.CourseEnrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.CourseId == request.CourseId
                && e.StudentUserId == _currentUser.UserId.Value
                && e.Status == CourseEnrollmentStatus.Active, cancellationToken);
        if (enrollment == null) return Result<CourseProgressDto>.Failure("Active enrollment not found");

        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Sections.OrderBy(s => s.SortOrder))
                .ThenInclude(s => s.Lectures.OrderBy(l => l.SortOrder))
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course == null) return Result<CourseProgressDto>.Failure("Course not found");

        var progressList = await _context.LectureProgresses
            .AsNoTracking()
            .Where(p => p.EnrollmentId == enrollment.Id)
            .ToListAsync(cancellationToken);

        var totalLectures = course.Sections.Sum(s => s.Lectures.Count);
        var completedLectures = progressList.Count(p => p.IsCompleted);

        var sections = course.Sections.Select(s => new SectionProgressDto(
            s.Id, s.Title,
            s.Lectures.Select(l =>
            {
                var progress = progressList.FirstOrDefault(p => p.LectureId == l.Id);
                return new LectureProgressDto(
                    l.Id, l.Title,
                    progress?.IsCompleted ?? false,
                    progress?.WatchedSec ?? 0,
                    progress?.LastPositionSec ?? 0);
            }).ToList()
        )).ToList();

        return Result<CourseProgressDto>.Success(
            new CourseProgressDto(enrollment.CompletionPercentage, totalLectures, completedLectures, sections));
    }
}
